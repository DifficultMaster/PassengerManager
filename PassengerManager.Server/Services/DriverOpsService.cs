using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Abstractions;
using PassengerManager.Server.Extensions;
using PassengerManager.Server.Hubs;
using PassengerManager.Server.Models;
using PassengerManager.Server.Protos.Static;
using PassengerManager.Server.Services.Events;
using PassengerManager.Server.Services.Interfaces;
using PassengerManager.Shared.DTOs;
using PassengerManager.Shared.Models;
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
        private readonly IMessageService _messageService;

        public DriverOpsService(
            ILogger<DriverOpsService> logger, 
            PassengerManagerContext context,
            INotificationService notifier,
            IConnectionMultiplexer redis,
            IMessageService messageService,
            int tripInfoRelevanceHours = 12)
        {
            _logger = logger;
            _context = context;
            _notifier = notifier;
            _redis = redis;
            _messageService = messageService;
            _tripInfoRelevanceHours = tripInfoRelevanceHours;
        }

        private record CachedRouteOption(string RouteId, string ShortName, string LongName, List<CachedTripOption> Trips);

        private record CachedTripOption(string TripId, string Headsign, int DirectionId);

        private static int MapToGtfsCause(IncidentType type)
        {
            // GTFS spec: https://gtfs.org/realtime/reference/#enum-cause
            return type switch
            {
                IncidentType.Other => 2,      // OTHER_CAUSE
                IncidentType.TechnicalProblem => 3, // TECHNICAL_PROBLEM
                IncidentType.Strike => 4, // STRIKE
                IncidentType.Demonstration => 5, // DEMONSTRATION
                IncidentType.Accident => 6, // ACCIDENT
                IncidentType.Holiday => 7, // HOLIDAY
                IncidentType.Weather => 8, // WEATHER
                IncidentType.Maintenance => 9, // MAINTENANCE
                IncidentType.Construction => 10, // CONSTRUCTION
                IncidentType.PoliceActivity => 11, // POLICE_ACTIVITY
                IncidentType.MedicalEmergency => 12, // MEDICAL_EMERGENCY
                _ => 1                          // UNKNOWN CAUSE
            };
        }

        private static int MapToGtfsEffect(IncidentType type)
        {
            // GTFS spec: https://gtfs.org/realtime/reference/#enum-effect
            return type switch
            {
                _ => 8                          // UNKNOWN EFFECT
            };
        }        

        public override async Task<GetManifestResponse> GetManifest(GetManifestRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();

            GetManifestResponse response = new GetManifestResponse
            {
                Success = true,
                Message = "Manifest retrieved successfully",
                Code = DriverOpsResultCode.Success
            };

            try
            {
                IDatabase db = _redis.GetDatabase();
                ClaimsPrincipal user = context.GetHttpContext().User;

                string agencyId = user.FindFirst("AgencyId")?.Value ?? "";                 
                string cacheKey = $"manifest:agency:{agencyId}:routes";
                RedisValue cachedData = await db.StringGetAsync(cacheKey);

                List<RouteOptionDto> routesToProcess;
                if (cachedData.HasValue)
                {
                    routesToProcess = JsonSerializer.Deserialize<List<RouteOptionDto>>((string)cachedData!)
                        ?? new List<RouteOptionDto>();
                }
                else
                {
                    routesToProcess = await _context.Routes
                        .AsNoTracking()
                        .Where(r => r.AgencyId == agencyId)
                        .Select(r => new RouteOptionDto(
                            r.RouteId,
                            r.ShortName,
                            r.LongName ?? r.ShortName,
                            r.Trips.Select(t => new TripOptionDto(
                                t.TripId,
                                t.Headsign ?? "Unknown",
                                t.DirectionId ?? 0
                                )).Distinct().ToList()
                            ))
                        .ToListAsync();

                    try
                    {
                        await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(routesToProcess),
                            TimeSpan.FromHours(_tripInfoRelevanceHours), flags: CommandFlags.FireAndForget);
                    }
                    catch (RedisConnectionException ex) 
                    {
                        _logger.LogWarning(ex, "Failed to write to Redis cache during GetManifest");
                    }
                }     
                
                foreach (RouteOptionDto route in routesToProcess)
                {
                    RouteOption option = new RouteOption
                    {
                        RouteId = route.RouteId,
                        ShortName = route.ShortName,
                        LongName = route.LongName
                    };

                    option.Trips.AddRange(route.Trips.Select(t => new TripOption
                    {
                        TripId = t.TripId,
                        Headsign = t.Headsign,
                        DirectionId = t.DirectionId
                    }));

                    response.Routes.Add(option);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DriverOpsService during GetManifest");

                response.Success = false;
                response.Message = "Internal server error";
                response.Code = DriverOpsResultCode.Unknown;
            }

            return response;
        }

        public override async Task<EndShiftResponse> EndShift(EndShiftRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();

            ClaimsPrincipal? principal = null;
            long shiftId = 0;
            DateTime endDateTime = DateTime.UtcNow;
            EndShiftResponse response = new EndShiftResponse
            {
                Success = false,
                Message = "Internal server error",
                Code = DriverOpsResultCode.Unknown
            };

            try
            {
                principal = context.GetHttpContext().User;
                shiftId = principal.GetShiftId();
                Shared.Models.Shift? shift = await _context.Shifts.FindAsync(shiftId);

                // CASE: Failure - Missing or invalid shift ID
                if (shiftId <= 0)
                {
                    response.Success = false;
                    response.Message = "Unauthorized: Invalid token state";
                    response.Code = DriverOpsResultCode.Unauthorized;                   
                }              

                // CASE: Success - Shift already closed
                else if (shift == null || shift.EndTime != null)
                {
                    response.Success = true;
                    response.Message = "Shift is closed";
                    response.Code = DriverOpsResultCode.ShiftInactive;                  
                }

                // CASE: Success - Shift will now be closed                
                else 
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    {
                        try
                        {
                            shift.EndTime = endDateTime;

                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();
                        }
                        catch
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }

                    response.Success = true;
                    response.Message = "Shift is closed";
                    response.Code = DriverOpsResultCode.Success;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DriverOpsService during EndShift");

                response.Success = false;
                response.Message = "Internal server error";
                response.Code = DriverOpsResultCode.Unknown;
            }
            finally
            {
                await _messageService.PublishSafeAsync(
                    new DriverOpsEvents.ShiftEnded(
                        UserId: principal?.GetUserId() ?? 0,
                        Success: response.Success,
                        Code: response.Code.ToString(),
                        EndDate: endDateTime,
                        Role: principal?.FindFirst(ClaimTypes.Role)?.Value,
                        VehicleId: principal?.GetVehicleId(),
                        AgencyId: principal?.FindFirst("AgencyId")?.Value,
                        ShiftId: shiftId > 0 ? shiftId : null),
                    "DriverOps.ShiftEnded",
                    context.CancellationToken
                    );
            }

            return response;
        }

        public override async Task<ReportIncidentResponse> ReportIncident(ReportIncidentRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();

            ReportIncidentResponse response = new ReportIncidentResponse
            {
                Success = false
            };

            ClaimsPrincipal? principal = null;
            long shiftId = 0;
            string? vehicleId = null;
            string? agencyId = null;
            string? routeId = null;
            string? alertId = null;
            int? gtfsCause = null;
            int? gtfsEffect = null;
            string? failureReason = "Unhandled exception";

            try
            {
                principal = context.GetHttpContext().User;
                shiftId = principal.GetShiftId();

                // CASE: Failure - Missing or invalid shift ID
                if (shiftId <= 0)
                {
                    response.Success = false;
                    failureReason = "Invalid shift token state";
                }
                else
                {
                    Shared.Models.Shift? shift = await _context.Shifts
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Id == shiftId);

                    // CASE: Failure - Shift not found
                    if (shift == null || shift.EndTime != null)
                    {
                        response.Success = false;
                        failureReason = "Shift is inactive";
                    }
                    else
                    {
                        vehicleId = shift.VehicleId;
                        routeId = await _context.Telemetries
                            .AsNoTracking()
                            .Where(t => t.VehicleId == shift.VehicleId && !string.IsNullOrEmpty(t.RouteId))
                            .OrderByDescending(t => t.Timestamp)
                            .Select(t => t.RouteId)
                            .FirstOrDefaultAsync();

                        if (string.IsNullOrWhiteSpace(routeId))
                        {
                            response.Success = false;
                            failureReason = "Route could not be resolved from telemetry";
                        }
                        else
                        {
                            agencyId = await _context.Routes
                                .AsNoTracking()
                                .Where(r => r.RouteId == routeId)
                                .Select(r => r.AgencyId)
                                .FirstOrDefaultAsync() ?? string.Empty;

                            gtfsCause = MapToGtfsCause(request.Type);
                            gtfsEffect = MapToGtfsEffect(request.Type);

                            Shared.Models.ServiceAlert alert = new Shared.Models.ServiceAlert
                            {
                                AlertId = Guid.NewGuid().ToString(),
                                RouteId = routeId,
                                AgencyId = agencyId,
                                Cause = gtfsCause.Value,
                                Effect = gtfsEffect.Value,
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
                                    routeId: routeId,
                                    type: request.Type.ToString()
                                    );

                            alertId = alert.AlertId;
                            response.Success = true;
                            failureReason = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DriverOpsService during ReportIncident");

                response.Success = false;
                failureReason = "Unhandled exception";
            }
            finally
            {
                await _messageService.PublishSafeAsync(
                    new DriverOpsEvents.IncidentReported(
                        UserId: principal?.GetUserId() ?? 0,
                        Success: response.Success,
                        OccurredAtUtc: DateTime.UtcNow,
                        IncidentType: request.Type.ToString(),
                        ShiftId: shiftId > 0 ? shiftId : null,
                        VehicleId: vehicleId ?? principal?.GetVehicleId(),
                        AgencyId: agencyId,
                        RouteId: routeId,
                        AlertId: alertId,
                        GtfsCause: gtfsCause,
                        GtfsEffect: gtfsEffect,
                        FailureReason: response.Success ? null : failureReason),
                    "DriverOps.IncidentReported",
                    context.CancellationToken);
            }

            return response;
        }

        public override async Task<GetTripShapeResponse> GetTripShape(GetTripShapeRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();

            GetTripShapeResponse response = new GetTripShapeResponse();

            try
            {
                if (string.IsNullOrWhiteSpace(request.TripId))
                {
                    return response;
                }

                Trip? trip = await _context.Trips
                    .AsNoTracking()
                    .Include(t => t.Shape)
                    .ThenInclude(s => s.ShapePoints)
                    .Include(t => t.Route)
                    .FirstOrDefaultAsync(t => t.TripId == request.TripId);

                if (trip?.Shape != null)
                {
                    response.Points.AddRange(trip.Shape.ShapePoints
                        .OrderBy(sp => sp.Sequence)
                        .Select(sp => new PassengerManager.Shared.Protos.ShapePoint
                        {
                            Latitude = sp.Latitude,
                            Longitude = sp.Longitude,
                            Sequence = sp.Sequence
                        }));

                    response.ColorHex = trip.Route?.Color ?? "#0000FF";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DriverOpsService during GetTripShape for trip {TripId}", request.TripId);
            }

            return response;
        }
    }
}
