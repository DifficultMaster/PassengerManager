
using Google.Protobuf.WellKnownTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Writers;
using PassengerManager.Server.Models;
using PassengerManager.Shared.DTOs;
using PassengerManager.Shared.Models;

namespace PassengerManager.Server.Services.Background
{
    public class GtfsArchiveWorker : BackgroundService
    {
        private readonly TelemetryChannels _channels;
        private readonly IServiceProvider _serviceProvider;
        private readonly GtfsScaleSettings _scaleSettings;
        private readonly ILogger<GtfsArchiveWorker> _logger;

        public GtfsArchiveWorker(
            TelemetryChannels channels,
            IServiceProvider serviceProdiver,
            GtfsScaleSettings scaleSettings,
            ILogger<GtfsArchiveWorker> logger)
        {
            _channels = channels;
            _serviceProvider = serviceProdiver;
            _scaleSettings = scaleSettings;
            _logger = logger;
        }

        private async Task ProcessVehicleChannelAsync(CancellationToken token)
        {
            Dictionary<string, VehiclePositionDto> latest = new Dictionary<string, VehiclePositionDto>();
            DateTime lastSaveTime = DateTime.UtcNow;

            await foreach (VehiclePositionDto dto in _channels.VehicleChannel.Reader.ReadAllAsync(token))
            {
                latest[dto.VehicleId] = dto;

                if (latest.Any() && (latest.Count >= _scaleSettings.ArchiverBatchSize || (DateTime.UtcNow - lastSaveTime).TotalSeconds > _scaleSettings.ArchiverMaxWaitSeconds))
                {
                    await SaveVehiclesToDatabaseAsync(latest.Values.ToList(), token);
                    latest.Clear();
                    lastSaveTime = DateTime.UtcNow;
                }
            }
        }

        private async Task ProcessTripChannelAsync(CancellationToken token)
        {
            Dictionary<string, TripUpdateDto> latest = new Dictionary<string, TripUpdateDto>();
            DateTime lastSaveTime = DateTime.UtcNow;

            await foreach (TripUpdateDto dto in _channels.TripChannel.Reader.ReadAllAsync(token))
            {
                latest[dto.TripId] = dto;

                if (latest.Any() && (latest.Count >= _scaleSettings.ArchiverBatchSize || (DateTime.UtcNow - lastSaveTime).TotalSeconds > _scaleSettings.ArchiverMaxWaitSeconds))
                {
                    await SaveTripsToDatabaseAsync(latest.Values.ToList(), token);
                    latest.Clear();
                    lastSaveTime = DateTime.UtcNow;
                }
            }
        }

        private async Task ProcessAlertChannelAsync(CancellationToken token)
        {
            Dictionary<string, ServiceAlertDto> latest = new Dictionary<string, ServiceAlertDto>();
            DateTime lastSaveTime = DateTime.UtcNow;

            await foreach (ServiceAlertDto dto in _channels.AlertChannel.Reader.ReadAllAsync(token))
            {
                latest[dto.AlertId] = dto;

                if (latest.Any() && (latest.Count >= _scaleSettings.ArchiverBatchSize || (DateTime.UtcNow - lastSaveTime).TotalSeconds > _scaleSettings.ArchiverMaxWaitSeconds))
                {
                    await SaveAlertsToDatabaseAsync(latest.Values.ToList(), token);
                    latest.Clear();
                    lastSaveTime = DateTime.UtcNow;
                }
            }
        }

        private async Task SaveVehiclesToDatabaseAsync(List<VehiclePositionDto> batch, CancellationToken token)
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            PassengerManagerContext context = scope.ServiceProvider.GetRequiredService<PassengerManagerContext>();

            // Extract distinct constraints
            List<string> incomingVehicleIds = batch.Select(b => b.VehicleId).Distinct().ToList();
            List<string> incomingPlates = batch
                .Where(b => !string.IsNullOrWhiteSpace(b.LicensePlate))
                .Select(b => b.LicensePlate!)
                .Distinct()
                .ToList();
            List<string> incomingRouteIds = batch
                .Where(b => !string.IsNullOrWhiteSpace(b.RouteId))
                .Select(b => b.RouteId!)
                .Distinct()
                .ToList();

            HashSet<string> existingRouteIds = await context.Routes
                .Where(r => incomingRouteIds.Contains(r.RouteId))
                .Select(r => r.RouteId)
                .ToHashSetAsync(token);

            // Fetch existing vehicles by ID OR License Plate to prevent unique constraint violations
            List<Shared.Models.Vehicle> existingVehiclesList = await context.Vehicles
                .Where(v => incomingVehicleIds.Contains(v.VehicleId) ||
                            (!string.IsNullOrEmpty(v.LicensePlate) && incomingPlates.Contains(v.LicensePlate)))
                .ToListAsync(token);

            Dictionary<string, Shared.Models.Vehicle> existingVehiclesById = existingVehiclesList
                .GroupBy(v => v.VehicleId)
                .ToDictionary(g => g.Key, g => g.First());

            // Keep a running tally of plates that are known to be taken so they aren't duplicated
            HashSet<string> takenLicensePlates = existingVehiclesList
                .Where(v => !string.IsNullOrEmpty(v.LicensePlate))
                .Select(v => v.LicensePlate!)
                .ToHashSet();

            List<Shared.Models.Vehicle> newVehicles = new List<PassengerManager.Shared.Models.Vehicle>();
            List<Shared.Models.Vehicle> vehiclesToUpdate = new List<PassengerManager.Shared.Models.Vehicle>();

            // Group by VehicleId to prevent in-batch duplicates (taking the latest update)
            var uniqueVehiclesInBatch = batch
                .GroupBy(b => b.VehicleId)
                .Select(g => g.Last())
                .ToList();

            foreach (VehiclePositionDto dto in uniqueVehiclesInBatch)
            {
                if (existingVehiclesById.TryGetValue(dto.VehicleId, out Shared.Models.Vehicle? existingVehicle))
                {
                    // Update plate only if it changed AND it isn't already taken by another vehicle
                    if (!string.IsNullOrEmpty(dto.LicensePlate) &&
                        existingVehicle.LicensePlate != dto.LicensePlate &&
                        !takenLicensePlates.Contains(dto.LicensePlate))
                    {
                        existingVehicle.LicensePlate = dto.LicensePlate;
                        takenLicensePlates.Add(dto.LicensePlate); // Mark as taken

                        if (!vehiclesToUpdate.Contains(existingVehicle))
                            vehiclesToUpdate.Add(existingVehicle);
                    }
                }
                else
                {
                    // Brand new vehicle. Ensure we don't assign a taken license plate.
                    string? safeLicensePlate = null;
                    if (!string.IsNullOrEmpty(dto.LicensePlate) && !takenLicensePlates.Contains(dto.LicensePlate))
                    {
                        safeLicensePlate = dto.LicensePlate;
                        takenLicensePlates.Add(safeLicensePlate); // Mark as taken
                    }

                    Shared.Models.Vehicle newVehicle = new PassengerManager.Shared.Models.Vehicle
                    {
                        VehicleId = dto.VehicleId,
                        LicensePlate = safeLicensePlate // Will be null if plate was already taken, preventing crash
                    };

                    newVehicles.Add(newVehicle);
                    existingVehiclesById[dto.VehicleId] = newVehicle;
                }
            }
         
            List<Shared.Models.Telemetry> newTelemetries = batch.Select(dto => new PassengerManager.Shared.Models.Telemetry
            {
                VehicleId = dto.VehicleId,
                RouteId = !string.IsNullOrWhiteSpace(dto.RouteId) && existingRouteIds.Contains(dto.RouteId)
                    ? dto.RouteId
                    : null,
                TripId = dto.TripId,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                Speed = dto.Speed,
                Bearing = dto.Bearing,
                Odometer = dto.Odometer,
                CurrentStatus = dto.CurrentStatus,
                StopId = dto.StopId,
                CurrentStopSequence = dto.CurrentStopSequence,
                CongestionLevel = dto.CongestionLevel,
                OccupancyStatus = dto.OccupancyStatus,
                Timestamp = dto.Timestamp
            }).ToList();
            
            bool autoDetectChanges = context.ChangeTracker.AutoDetectChangesEnabled;
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, token);
            try
            {
                if (newVehicles.Any())
                    await context.Vehicles.AddRangeAsync(newVehicles, token);

                foreach (Shared.Models.Vehicle vehicle in vehiclesToUpdate)
                {
                    context.Entry(vehicle).Property(v => v.LicensePlate).IsModified = true;
                }

                if (newTelemetries.Any())
                    await context.Telemetries.AddRangeAsync(newTelemetries, token);

                await context.SaveChangesAsync(token);
                await transaction.CommitAsync(token);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(token);
                _logger.LogError(ex, "Failed to save vehicle batch to database.");
            }
            finally
            {
                context.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
                context.ChangeTracker.Clear();
            }
        }

        private async Task SaveTripsToDatabaseAsync(List<TripUpdateDto> batch, CancellationToken token)
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            PassengerManagerContext context = scope.ServiceProvider.GetRequiredService<PassengerManagerContext>();

            List<TripUpdate> newTripUpdates = batch.Select(dto => new PassengerManager.Shared.Models.TripUpdate
            {
                TripId = dto.TripId,
                VehicleId = dto.VehicleId,
                Timestamp = dto.Timestamp,
                DelaySeconds = dto.DelaySeconds
            }).ToList();

            List<string> incomingTripIds = batch.Select(t => t.TripId).Distinct().ToList();
            List<string> incomingVehicleIds = newTripUpdates.Where(t => t.VehicleId != null).Select(t => t.VehicleId!).Distinct().ToList();

            HashSet<string> existingTripIds = await context.Trips.Where(t => incomingTripIds.Contains(t.TripId)).Select(t => t.TripId).ToHashSetAsync(token);
            HashSet<string> existingVehicleIds = await context.Vehicles.Where(v => incomingVehicleIds.Contains(v.VehicleId)).Select(v => v.VehicleId).ToHashSetAsync(token);

            List<TripUpdate> validTripUpdates = newTripUpdates.Where(tu =>
                existingTripIds.Contains(tu.TripId) &&
                (tu.VehicleId == null || existingVehicleIds.Contains(tu.VehicleId))
            ).ToList();

            if (!validTripUpdates.Any()) 
                return;

            bool autoDetect = context.ChangeTracker.AutoDetectChangesEnabled;
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, token);
            try
            {
                await context.TripUpdates.AddRangeAsync(validTripUpdates, token);
                await context.SaveChangesAsync(token);
                await transaction.CommitAsync(token);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(token);
                _logger.LogError(ex, "Transaction failed for Trip Updates");
            }
            finally
            {
                context.ChangeTracker.AutoDetectChangesEnabled = autoDetect;
                context.ChangeTracker.Clear();
            }
        }

        private async Task SaveAlertsToDatabaseAsync(List<ServiceAlertDto> batch, CancellationToken token)
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            PassengerManagerContext context = scope.ServiceProvider.GetRequiredService<PassengerManagerContext>();

            List<string> incomingAlertIds = batch.Select(b => b.AlertId).Distinct().ToList();
            HashSet<string> existingAlertIds = await context.ServiceAlerts.Where(a => incomingAlertIds.Contains(a.AlertId)).Select(a => a.AlertId).ToHashSetAsync(token);

            List<ServiceAlert> newAlerts = new List<PassengerManager.Shared.Models.ServiceAlert>();
            List<ServiceAlert> updatedAlerts = new List<PassengerManager.Shared.Models.ServiceAlert>();

            foreach (ServiceAlertDto dto in batch)
            {
                ServiceAlert alert = new PassengerManager.Shared.Models.ServiceAlert
                {
                    AlertId = dto.AlertId,
                    AgencyId = dto.AgencyId,
                    RouteId = dto.RouteId,
                    StopId = dto.StopId,
                    HeaderText = dto.HeaderText,
                    DescriptionText = dto.DescriptionText,
                    Cause = dto.Cause,
                    Effect = dto.Effect,
                    StartTime = dto.StartTime,
                    EndTime = dto.EndTime,
                    IsActive = dto.IsActive
                };

                if (existingAlertIds.Contains(dto.AlertId)) 
                    updatedAlerts.Add(alert);
                else 
                    newAlerts.Add(alert);
            }

            HashSet<string> existingAgencyIds = await context.Agencies.Select(a => a.AgencyId).ToHashSetAsync(token);
            HashSet<string> existingRouteIds = await context.Routes.Select(r => r.RouteId).ToHashSetAsync(token);
            HashSet<string> existingStopIds = await context.Stops.Select(s => s.StopId).ToHashSetAsync(token);

            bool IsValid(PassengerManager.Shared.Models.ServiceAlert a) =>
                (a.AgencyId == null || existingAgencyIds.Contains(a.AgencyId)) &&
                (a.RouteId == null || existingRouteIds.Contains(a.RouteId)) &&
                (a.StopId == null || existingStopIds.Contains(a.StopId));

            newAlerts = newAlerts.Where(IsValid).ToList();
            updatedAlerts = updatedAlerts.Where(IsValid).ToList();

            if (!newAlerts.Any() && !updatedAlerts.Any()) return;

            bool autoDetect = context.ChangeTracker.AutoDetectChangesEnabled;
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, token);
            try
            {
                if (newAlerts.Any()) await context.ServiceAlerts.AddRangeAsync(newAlerts, token);

                foreach (ServiceAlert alert in updatedAlerts)
                {
                    ServiceAlert? existing = await context.ServiceAlerts.FindAsync(new object[] { alert.AlertId }, token);
                    if (existing != null) 
                        context.Entry(existing).CurrentValues.SetValues(alert);
                }

                await context.SaveChangesAsync(token);
                await transaction.CommitAsync(token);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(token);
                _logger.LogError(ex, "Transaction failed for Service Alerts");
            }
            finally
            {
                context.ChangeTracker.AutoDetectChangesEnabled = autoDetect;
                context.ChangeTracker.Clear();
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.WhenAll(
                ProcessVehicleChannelAsync(stoppingToken), 
                ProcessTripChannelAsync(stoppingToken), 
                ProcessAlertChannelAsync(stoppingToken));
        }
    }
}
