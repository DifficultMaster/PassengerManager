using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PassengerManager.Server.Extensions;
using PassengerManager.Server.Hubs;
using PassengerManager.Server.Models;
using PassengerManager.Server.Protos.Static;
using PassengerManager.Server.Services.Interfaces;
using PassengerManager.Shared.Protos;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text.Json;

namespace PassengerManager.Server.Services
{
    [Authorize(Roles = "Driver")]
    public class DriverOpsService : PassengerManager.Shared.Protos.DriverOpsService.DriverOpsServiceBase
    {
        private readonly ILogger<DriverOpsService> _logger;
        private readonly PassengerManagerContext _context;
        private readonly INotificationService _notifier;
        private readonly IConnectionMultiplexer _redis;
        private readonly int _tripInfoRelevanceHours;

        public DriverOpsService(
            ILogger<DriverOpsService> logger, 
            PassengerManagerContext context,
            INotificationService notifier,
            IConnectionMultiplexer redis,
            int tripInfoRelevanceHours = 12)
        {
            _logger = logger;
            _context = context;
            _notifier = notifier;
            _redis = redis;
            _tripInfoRelevanceHours = tripInfoRelevanceHours;
        }

        private record CachedTripOption(string TripId, string Headsign, int DirectionId);

        private static int MapToGtfsCause(IncidentType type)
        {
            // GTFS spec: https://gtfs.org/realtime/reference/#enum-cause
            return type switch
            {
                IncidentType.Traffic => 2,      // OTHER CAUSE
                IncidentType.Accident => 6,     // ACCIDENT
                IncidentType.Breakdown => 9,    // MAINTENANCE
                IncidentType.Detour => 11,      // POLICE_ACTIVITY
                IncidentType.Emergency => 12,   // MEDICAL_EMERGENCY                
                _ => 1                          // UNKNOWN CAUSE
            };
        }

        private static int MapToGtfsEffect(IncidentType type)
        {
            // GTFS spec: https://gtfs.org/realtime/reference/#enum-effect
            return type switch
            {
                IncidentType.Traffic => 3,      // SIGNIFICANT DELAYS      
                IncidentType.Breakdown => 2,    // REDUCED_SERVICE
                _ => 8                          // UNKNOWN_EFFECT
            };
        }        

        public override async Task<SetShiftRouteResponse> SetShiftRoute(SetShiftRouteRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();

            try
            {
                ClaimsPrincipal user = context.GetHttpContext().User;
                long shiftId = user.GetShiftId();

                // CASE: Failure - Missing or invalid shift ID
                if (shiftId <= 0)
                {
                    return new SetShiftRouteResponse
                    {
                        Success = false,
                        Message = "Unauthorized: Invalid token state",
                        Code = DriverOpsResultCode.Unauthorized
                    };
                }

                Shared.Models.Shift? shift = await _context.Shifts.FindAsync(shiftId);

                // CASE: Failure - Shift not found
                if (shift == null || shift.EndTime != null)
                {
                    return new SetShiftRouteResponse
                    {
                        Success = false,
                        Message = "Shift is closed",
                        Code = DriverOpsResultCode.ShiftInactive
                    };
                }

                // CASE: Success - route already set to this shift
                if (shift.RouteId == request.RouteId)
                {
                    return new SetShiftRouteResponse
                    {
                        Success = true,
                        Message = "Route set successfully",
                        Code = DriverOpsResultCode.Success
                    };
                }

                // CASE: Success - route will now be set to this shift
                using var transaction = await _context.Database.BeginTransactionAsync();
                {
                    try
                    {
                        shift.RouteId = request.RouteId;
                        shift.CurrentTripId = null;

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }

                return new SetShiftRouteResponse
                {
                    Success = true,
                    Message = "Route set successfully",
                    Code = DriverOpsResultCode.Success
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DriverOpsService during SetShiftRoute");

                return new SetShiftRouteResponse
                {
                    Success = false,
                    Message = "Internal server error",
                    Code = DriverOpsResultCode.Unknown
                };
            }
        }

        public override async Task<GetRouteTripsResponse> GetRouteTrips(GetRouteTripsRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();

            try
            {
                GetRouteTripsResponse response = new GetRouteTripsResponse
                {
                    Success = true,
                    Message = "Trips retrieved successfully",
                    Code = DriverOpsResultCode.Success
                };

                IDatabase db = _redis.GetDatabase();
                string cacheKey = $"trips:route:{request.RouteId}";
                RedisValue cachedData = await db.StringGetAsync(cacheKey);

                if (cachedData.HasValue)
                {
                    var cachedOptions = JsonSerializer.Deserialize<List<CachedTripOption>>((string)cachedData!);
                    if (cachedOptions != null)
                    {
                        foreach (CachedTripOption option in cachedOptions)
                        {
                            response.Options.Add(new RouteTripOption
                            { 
                                TripId = option.TripId,
                                Headsign = option.Headsign,
                                DirectionId = option.DirectionId
                            });
                        }

                        return response;
                    }
                }

                List<Shared.Models.Trip> distinctTrips = await _context.Trips
                    .AsNoTracking()
                    .Where(t => t.RouteId == request.RouteId)
                    .GroupBy(t => new { t.Headsign, t.DirectionId })
                    .Select(g => g.First())
                    .ToListAsync();

                List<CachedTripOption> cacheListToSave = new List<CachedTripOption>();
                foreach (Shared.Models.Trip trip in distinctTrips)
                {
                    response.Options.Add(new RouteTripOption
                    {
                        TripId = trip.TripId,
                        Headsign = trip.Headsign ?? "Unknown",
                        DirectionId = trip.DirectionId ?? 0
                    });

                    cacheListToSave.Add(new CachedTripOption(
                        trip.TripId,
                        trip.Headsign ?? "Unknown",
                        trip.DirectionId ?? 0
                    ));

                    try
                    {
                        await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(cacheListToSave), 
                            TimeSpan.FromHours(_tripInfoRelevanceHours), flags: CommandFlags.FireAndForget);
                    }
                    catch (RedisConnectionException) { }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DriverOpsService during GetRouteTrips");

                return new GetRouteTripsResponse
                {
                    Success = false,
                    Message = "Internal server error",
                    Code = DriverOpsResultCode.Unknown
                };
            }
        }

        public override async Task<SetShiftTripResponse> SetShiftTrip(SetShiftTripRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();

            try
            {
                ClaimsPrincipal user = context.GetHttpContext().User;
                long shiftId = user.GetShiftId();

                // CASE: Failure - Missing or invalid shift ID
                if (shiftId <= 0)
                {
                    return new SetShiftTripResponse
                    {
                        Success = false,
                        Message = "Unauthorized: Invalid token state",
                        Code = DriverOpsResultCode.Unauthorized
                    };
                }

                Shared.Models.Shift? shift = await _context.Shifts.FindAsync(shiftId);

                // CASE: Failure - Shift not found
                if (shift == null || shift.EndTime != null)
                {
                    return new SetShiftTripResponse
                    {
                        Success = false,
                        Message = "Shift is closed",
                        Code = DriverOpsResultCode.ShiftInactive
                    };
                }

                // CASE: Success - trip already set to this shift
                if (shift.CurrentTripId == request.TripId)
                {
                    return new SetShiftTripResponse
                    {
                        Success = true,
                        Message = "Trip set successfully",
                        Code = DriverOpsResultCode.Success
                    };
                }

                // CASE: Success - trip will now be set to this shift
                using var transaction = await _context.Database.BeginTransactionAsync();
                {
                    try
                    {
                        shift.CurrentTripId = request.TripId;

                        if (string.IsNullOrEmpty(shift.RouteId))
                        {
                            Shared.Models.Trip? trip = await _context.Trips.FindAsync(request.TripId);

                            if (trip != null)
                            {
                                shift.RouteId = trip.RouteId;
                            }
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }

                return new SetShiftTripResponse
                {
                    Success = true,
                    Message = "Trip set successfully",
                    Code = DriverOpsResultCode.Success
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DriverOpsService during SetShiftTrip");
                return new SetShiftTripResponse
                {
                    Success = false,
                    Message = "Internal server error",
                    Code = DriverOpsResultCode.Unknown
                };
            }
        }

        public override async Task<EndShiftResponse> EndShift(EndShiftRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();

            try
            {
                ClaimsPrincipal user = context.GetHttpContext().User;
                long shiftId = user.GetShiftId();

                // CASE: Failure - Missing or invalid shift ID
                if (shiftId <= 0)
                {
                    return new EndShiftResponse
                    {
                        Success = false,
                        Message = "Unauthorized: Invalid token state",
                        Code = DriverOpsResultCode.Unauthorized
                    };
                }

                Shared.Models.Shift? shift = await _context.Shifts.FindAsync(shiftId);

                // CASE: Success - Shift already closed
                if (shift == null || shift.EndTime != null)
                {
                    return new EndShiftResponse
                    {
                        Success = true,
                        Message = "Shift is closed",
                        Code = DriverOpsResultCode.ShiftInactive
                    };
                }

                // CASE: Success - Shift will now be closed
                using var transaction = await _context.Database.BeginTransactionAsync();
                {
                    try
                    {
                        shift.EndTime = DateTime.UtcNow;

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }

                return new EndShiftResponse
                {
                    Success = true,
                    Message = "Shift is closed",
                    Code = DriverOpsResultCode.Success
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DriverOpsService during EndShift");

                return new EndShiftResponse
                {
                    Success = false,
                    Message = "Internal server error",
                    Code = DriverOpsResultCode.Unknown
                };
            }
        }

        public override async Task<HeartbeatResponse> SendHeartbeat(HeartbeatRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();

            ClaimsPrincipal user = context.GetHttpContext().User;
            long shiftId = user.GetShiftId();
            string vehicleId = user.GetVehicleId();

            // LOGIC FOR HEARTBEAT, will be coded later as I get to dispatcher's backend logic
            // Driver can generate Service Alerts
            // Server auto-generates Trip Updates

            return new HeartbeatResponse
            {
                Success = true
            };
        }

        public override async Task<ReportIncidentResponse> ReportIncident(ReportIncidentRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();

            try
            {
                ClaimsPrincipal user = context.GetHttpContext().User;
                long shiftId = user.GetShiftId();            

                // CASE: Failure - Missing or invalid shift ID
                if (shiftId <= 0)
                {
                    return new ReportIncidentResponse
                    {
                        Success = false
                    };
                }

                Shared.Models.Shift? shift = await _context.Shifts
                    .AsNoTracking()
                    .Include(s => s.Route)
                    .FirstOrDefaultAsync(s => s.Id == shiftId);

                // CASE: Failure - Shift not found
                if (shift == null || shift.EndTime != null)
                {
                    return new ReportIncidentResponse
                    {
                        Success = false
                    };
                }

                string agencyId = shift.Route?.AgencyId ?? string.Empty;

                int gtfsCause = MapToGtfsCause(request.Type);
                int gtfsEffect = MapToGtfsEffect(request.Type);

                Shared.Models.ServiceAlert alert = new Shared.Models.ServiceAlert
                {
                    AlertId = Guid.NewGuid().ToString(),
                    RouteId = shift.RouteId,
                    AgencyId = agencyId,
                    Cause = gtfsCause,
                    Effect = gtfsEffect,
                    HeaderText = request.Type.ToString(),
                    DescriptionText = request.Description,
                    IsActive = false,
                    StartTime = DateTime.Now
                };

                using var transaction = await _context.Database.BeginTransactionAsync();
                {
                    try
                    {
                        _context.ServiceAlerts.Add(alert);

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();                        
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }

                _ = _notifier.AlertDispatchersByAgency(
                        agencyId: agencyId,
                        alertId: alert.AlertId,
                        routeId: shift.RouteId,
                        type: request.Type.ToString()
                        );

                return new ReportIncidentResponse
                {
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DriverOpsService during ReportIncident");
                return new ReportIncidentResponse
                {
                    Success = false
                };
            }
        }
    }
}
