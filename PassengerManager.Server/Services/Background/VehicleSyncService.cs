using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using PassengerManager.Server.Models;
using PassengerManager.Server.Protos.Static;
using PassengerManager.Server.Services.Static;
using PassengerManager.Shared.Models;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace PassengerManager.Server.Services.Background
{
    public class VehicleSyncService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<VehicleSyncService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        private DateTimeOffset? _lastUpdated = null;
        private EntityTagHeaderValue? _lastEntityTag = null;

        public VehicleSyncService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<VehicleSyncService> logger, IHttpClientFactory httpClientFactory)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        private async Task ProcessFeed(FeedMessage feed, PassengerManagerContext context)
        {
            List<FeedEntity> entities = feed.Entity.Where(e => e.Vehicle != null).ToList();
            if (!entities.Any()) return;

            List<string> incomingIds = entities
                .Select(e => e.Vehicle.Vehicle.Id)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();

            Dictionary<string, Shared.Models.Vehicle> existingVehicles = await context.Vehicles
                .Where(v => incomingIds.Contains(v.VehicleId))
                .ToDictionaryAsync(v => v.VehicleId);

            List<Shared.Models.Vehicle> newVehicles = new List<Shared.Models.Vehicle>();
            List<Shared.Models.Telemetry> newTelemetries = new List<Shared.Models.Telemetry>();

            DateTime now = DateTime.UtcNow;

            foreach (FeedEntity entity in entities)
            {
                VehiclePosition gtfsVehicle = entity.Vehicle;
                if (gtfsVehicle.Vehicle == null || string.IsNullOrEmpty(gtfsVehicle.Vehicle.Id)) 
                    continue;

                string vehicleId = gtfsVehicle.Vehicle.Id;
                Shared.Models.Vehicle vehicle;

                if (existingVehicles.TryGetValue(vehicleId, out Vehicle? existingVehicle))
                {
                    vehicle = existingVehicle;
                }
                else
                {
                    vehicle = new Shared.Models.Vehicle
                    {
                        VehicleId = vehicleId
                    };

                    newVehicles.Add(vehicle);
                    existingVehicles[vehicleId] = vehicle;
                }

                if (gtfsVehicle.Vehicle.HasLicensePlate)
                    vehicle.LicensePlate = gtfsVehicle.Vehicle.LicensePlate;

                if (gtfsVehicle.Position != null)
                {
                    DateTime telemetryTimestamp = gtfsVehicle.HasTimestamp
                        ? DateTimeOffset.FromUnixTimeSeconds((long)gtfsVehicle.Timestamp).UtcDateTime
                        : now;

                    newTelemetries.Add(new Shared.Models.Telemetry
                    {
                        VehicleId = vehicleId,
                        RouteId = gtfsVehicle.Trip?.RouteId,
                        TripId = gtfsVehicle.Trip?.TripId,

                        Latitude = gtfsVehicle.Position.Latitude,
                        Longitude = gtfsVehicle.Position.Longitude,

                        Bearing = gtfsVehicle.Position.HasBearing ? gtfsVehicle.Position.Bearing : null,
                        Speed = gtfsVehicle.Position.HasSpeed ? gtfsVehicle.Position.Speed : null,
                        Odometer = gtfsVehicle.Position.HasOdometer ? gtfsVehicle.Position.Odometer : null,
                        CurrentStatus = gtfsVehicle.HasCurrentStatus ? (int?)gtfsVehicle.CurrentStatus : null,

                        StopId = string.IsNullOrWhiteSpace(gtfsVehicle.StopId) ? null : gtfsVehicle.StopId,
                        CurrentStopSequence = gtfsVehicle.HasCurrentStopSequence ? (int?)gtfsVehicle.CurrentStopSequence : null,
                        CongestionLevel = gtfsVehicle.HasCongestionLevel ? (int?)gtfsVehicle.CongestionLevel : null,
                        OccupancyStatus = gtfsVehicle.HasOccupancyStatus ? (int?)gtfsVehicle.OccupancyStatus : null,

                        Timestamp = telemetryTimestamp
                    });
                }
            }

            if (!newVehicles.Any() && !newTelemetries.Any())
            {
                return;
            }

            Dictionary<string, string?> routeAgencyMap = await context.Routes
                .Select(r => new { r.RouteId, r.AgencyId })
                .ToDictionaryAsync(r => r.RouteId, r => r.AgencyId);

            HashSet<string> existingRouteIds = routeAgencyMap.Keys.ToHashSet();

            HashSet<string> existingTripIds = (await context.Trips
                .Select(t => t.TripId)
                .ToListAsync())
                .ToHashSet();

            newTelemetries = newTelemetries
                .Where(t =>
                    (t.RouteId == null || existingRouteIds.Contains(t.RouteId)) &&
                    (t.TripId == null || existingTripIds.Contains(t.TripId)))
                .ToList();

            List<Shared.Models.Vehicle> vehiclesToUpdateAgency = new List<Shared.Models.Vehicle>();

            foreach (KeyValuePair<string, Shared.Models.Vehicle> kvp in existingVehicles)
            {
                Shared.Models.Vehicle vehicle = kvp.Value;
                if (!string.IsNullOrEmpty(vehicle.AgencyId)) continue;

                Shared.Models.Telemetry? telemetry = newTelemetries.FirstOrDefault(t => t.VehicleId == vehicle.VehicleId && t.RouteId != null);
                if (telemetry != null && routeAgencyMap.TryGetValue(telemetry.RouteId, out string? agencyId) && !string.IsNullOrEmpty(agencyId))
                {
                    vehicle.AgencyId = agencyId;
                    vehiclesToUpdateAgency.Add(vehicle);
                }
            }

            if (!newVehicles.Any() && !newTelemetries.Any() && !vehiclesToUpdateAgency.Any())
            {
                return;
            }

            bool autoDetectChanges = context.ChangeTracker.AutoDetectChangesEnabled;
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            try
            {
                if (newVehicles.Any())
                {
                    await context.Vehicles.AddRangeAsync(newVehicles);
                }

                foreach (Shared.Models.Vehicle vehicle in vehiclesToUpdateAgency)
                {
                    context.Entry(vehicle).Property(v => v.AgencyId).IsModified = true;
                }

                if (newTelemetries.Any())
                {
                    await context.Telemetries.AddRangeAsync(newTelemetries);
                }

                await context.SaveChangesAsync();
            }
            finally
            {
                context.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
                context.ChangeTracker.Clear();
            }
        }

        protected override async Task ExecuteAsync(CancellationToken token)
        {
            await Task.Yield();

            int interval = _configuration.GetValue<int>("GtfsSettings:VehicleDataSyncIntervalSeconds", AppDefaults.Sync.VehicleIntervalSeconds);
            bool isEnabled = _configuration.GetValue<bool>("GtfsSettings:VehicleDataAutoSyncEnabled", true);

            if (!isEnabled)
            {
                _logger.LogInformation("VehicleSyncService is disabled via config");
                return;
            }

            using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromSeconds(interval));
            while (await timer.WaitForNextTickAsync(token))
            {
                try
                {
                    await RunSync(token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception in VehicleSyncService.");
                }
            }
        }

        public async Task RunSync(CancellationToken token)
        {
            string url = _configuration.GetValue<string>("GtfsSettings:VehicleDataUrl") ?? string.Empty;
            if (string.IsNullOrEmpty(url)) return;

            HttpClient client = _httpClientFactory.CreateClient("GtfsClient");
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);

            if (_lastUpdated.HasValue)
            {
                request.Headers.IfModifiedSince = _lastUpdated;
            }
            if (_lastEntityTag != null)
            {
                request.Headers.IfNoneMatch.Add(_lastEntityTag);
            }

            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

            if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
            {
                return;
            }
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Failed to download GTFS realtime vehicle data. GTFS feed failed: {response.StatusCode}");
                return;
            }

            FeedMessage? feed = null;

            try
            {
                using MemoryStream memoryStream = new MemoryStream();
                await response.Content.CopyToAsync(memoryStream, token);

                memoryStream.Position = 0;
                feed = FeedMessage.Parser.ParseFrom(memoryStream);
            }
            catch (InvalidProtocolBufferException ex)
            {
                _logger.LogWarning($"Failed to download GTFS realtime vehicle data. Downloaded file is corrupt or not a valid GTFS Protobuf: {ex.Message}");
                return;
            }
            catch (IOException ex)
            {
                _logger.LogWarning($"Failed to download GTFS realtime vehicle data. Network interrupted during download: {ex.Message}");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to download GTFS realtime vehicle data: {ex.Message}");
                return;
            }

            using IServiceScope scope = _serviceProvider.CreateScope();
            PassengerManagerContext context = scope.ServiceProvider.GetRequiredService<PassengerManagerContext>();

            try
            {
                await ProcessFeed(feed, context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to process GTFS realtime vehicle data.");
            }
        }
    }
}
