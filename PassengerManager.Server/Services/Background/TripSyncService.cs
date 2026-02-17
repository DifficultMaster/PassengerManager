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
    public class TripSyncService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TripSyncService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        private DateTimeOffset? _lastUpdated = null;
        private EntityTagHeaderValue? _lastEntityTag = null;

        public TripSyncService(
            IServiceProvider serviceProvider, 
            IConfiguration configuration, 
            ILogger<TripSyncService> logger, 
            IHttpClientFactory httpClientFactory)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        private async Task ProcessTripUpdates(FeedMessage feed, PassengerManagerContext context)
        {
            List<FeedEntity> entities = feed.Entity.Where(e => e.TripUpdate != null).ToList();
            if (!entities.Any()) return;

            List<Shared.Models.TripUpdate> newTripUpdates = new List<Shared.Models.TripUpdate>();
            DateTime now = DateTime.UtcNow;

            foreach (FeedEntity entity in entities)
            {
                Protos.Static.TripUpdate gtfsTripUpdate = entity.TripUpdate;
                if (gtfsTripUpdate.Trip == null || string.IsNullOrEmpty(gtfsTripUpdate.Trip.TripId))
                    continue;

                string tripId = gtfsTripUpdate.Trip.TripId;
                string? vehicleId = gtfsTripUpdate.Vehicle?.Id;

                DateTime? timestamp = gtfsTripUpdate.HasTimestamp
                    ? DateTimeOffset.FromUnixTimeSeconds((long)gtfsTripUpdate.Timestamp).UtcDateTime
                    : now;

                int? delaySeconds = gtfsTripUpdate.HasDelay
                    ? gtfsTripUpdate.Delay
                    : gtfsTripUpdate.StopTimeUpdate
                        .LastOrDefault(s => s.Arrival != null && s.Arrival.HasDelay)?.Arrival?.Delay
                      ?? gtfsTripUpdate.StopTimeUpdate
                        .LastOrDefault(s => s.Departure != null && s.Departure.HasDelay)?.Departure?.Delay;

                newTripUpdates.Add(new Shared.Models.TripUpdate
                {
                    TripId = tripId,
                    VehicleId = string.IsNullOrEmpty(vehicleId) ? null : vehicleId,
                    Timestamp = timestamp,
                    DelaySeconds = delaySeconds
                });
            }

            if (!newTripUpdates.Any()) return;

            HashSet<string> existingTripIds = (await context.Trips
                .Select(t => t.TripId)
                .ToListAsync())
                .ToHashSet();

            HashSet<string> existingVehicleIds = (await context.Vehicles
                .Select(v => v.VehicleId)
                .ToListAsync())
                .ToHashSet();

            newTripUpdates = newTripUpdates
                .Where(tu =>
                    (tu.TripId == null || existingTripIds.Contains(tu.TripId)) &&
                    (tu.VehicleId == null || existingVehicleIds.Contains(tu.VehicleId)))
                .ToList();

            if (!newTripUpdates.Any()) return;

            bool autoDetectChanges = context.ChangeTracker.AutoDetectChangesEnabled;
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            try
            {
                await context.TripUpdates.AddRangeAsync(newTripUpdates);
                await context.SaveChangesAsync();
            }
            finally
            {
                context.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
                context.ChangeTracker.Clear();
            }
        }

        private async Task ProcessAlerts(FeedMessage feed, PassengerManagerContext context)
        {
            List<FeedEntity> entities = feed.Entity.Where(e => e.Alert != null).ToList();
            if (!entities.Any()) return;

            List<string> incomingAlertIds = entities
                .Select(e => e.Id)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();

            HashSet<string> existingAlertIds = (await context.ServiceAlerts
                .Where(a => incomingAlertIds.Contains(a.AlertId))
                .Select(a => a.AlertId)
                .ToListAsync())
                .ToHashSet();

            List<Shared.Models.ServiceAlert> newAlerts = new List<Shared.Models.ServiceAlert>();
            List<Shared.Models.ServiceAlert> updatedAlerts = new List<Shared.Models.ServiceAlert>();

            foreach (FeedEntity entity in entities)
            {
                Alert gtfsAlert = entity.Alert;
                string alertId = entity.Id;
                if (string.IsNullOrEmpty(alertId)) continue;

                EntitySelector? informed = gtfsAlert.InformedEntity.FirstOrDefault();

                string? headerText = gtfsAlert.HeaderText?.Translation.FirstOrDefault()?.Text;
                string? descriptionText = gtfsAlert.DescriptionText?.Translation.FirstOrDefault()?.Text;

                TimeRange? activePeriod = gtfsAlert.ActivePeriod.FirstOrDefault();
                DateTime? startTime = activePeriod != null && activePeriod.HasStart
                    ? DateTimeOffset.FromUnixTimeSeconds((long)activePeriod.Start).UtcDateTime
                    : null;
                DateTime? endTime = activePeriod != null && activePeriod.HasEnd
                    ? DateTimeOffset.FromUnixTimeSeconds((long)activePeriod.End).UtcDateTime
                    : null;

                bool isActive = endTime == null || endTime > DateTime.UtcNow;

                Shared.Models.ServiceAlert alert = new Shared.Models.ServiceAlert
                {
                    AlertId = alertId,
                    AgencyId = informed?.AgencyId,
                    RouteId = informed?.RouteId,
                    StopId = informed?.StopId,
                    HeaderText = headerText,
                    DescriptionText = descriptionText,
                    Cause = gtfsAlert.HasCause ? (int?)gtfsAlert.Cause : null,
                    Effect = gtfsAlert.HasEffect ? (int?)gtfsAlert.Effect : null,
                    StartTime = startTime,
                    EndTime = endTime,
                    IsActive = isActive
                };

                if (existingAlertIds.Contains(alertId))
                {
                    updatedAlerts.Add(alert);
                }
                else
                {
                    newAlerts.Add(alert);
                }
            }

            if (!newAlerts.Any() && !updatedAlerts.Any()) return;

            HashSet<string> existingAgencyIds = (await context.Agencies
                .Select(a => a.AgencyId)
                .ToListAsync())
                .ToHashSet();

            HashSet<string> existingRouteIds = (await context.Routes
                .Select(r => r.RouteId)
                .ToListAsync())
                .ToHashSet();

            HashSet<string> existingStopIds = (await context.Stops
                .Select(s => s.StopId)
                .ToListAsync())
                .ToHashSet();

            bool isFkValid(Shared.Models.ServiceAlert a) =>
                (a.AgencyId == null || existingAgencyIds.Contains(a.AgencyId)) &&
                (a.RouteId == null || existingRouteIds.Contains(a.RouteId)) &&
                (a.StopId == null || existingStopIds.Contains(a.StopId));

            newAlerts = newAlerts.Where(isFkValid).ToList();
            updatedAlerts = updatedAlerts.Where(isFkValid).ToList();

            if (!newAlerts.Any() && !updatedAlerts.Any()) return;

            bool autoDetectChanges = context.ChangeTracker.AutoDetectChangesEnabled;
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            try
            {
                if (newAlerts.Any())
                {
                    await context.ServiceAlerts.AddRangeAsync(newAlerts);
                }

                foreach (Shared.Models.ServiceAlert alert in updatedAlerts)
                {
                    Shared.Models.ServiceAlert? existing = await context.ServiceAlerts.FindAsync(alert.AlertId);
                    if (existing != null)
                    {
                        context.Entry(existing).CurrentValues.SetValues(alert);
                    }
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

            int interval = _configuration.GetValue<int>("GtfsSettings:TripDataSyncIntervalSeconds", AppDefaults.Sync.TripIntervalSeconds);
            bool isEnabled = _configuration.GetValue<bool>("GtfsSettings:TripDataAutoSyncEnabled", true);

            if (!isEnabled)
            {
                _logger.LogInformation("TripSyncService is disabled via config");
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
                    _logger.LogError(ex, "Unhandled exception in TripSyncService.");
                }
            }
        }

        public async Task RunSync(CancellationToken token)
        {
            string url = _configuration.GetValue<string>("GtfsSettings:TripDataUrl") ?? string.Empty;
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
                _logger.LogWarning($"Failed to download GTFS realtime trip data. GTFS feed failed: {response.StatusCode}");
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
                _logger.LogWarning($"Failed to download GTFS realtime trip data. Downloaded file is corrupt or not a valid GTFS Protobuf: {ex.Message}");
                return;
            }
            catch (IOException ex)
            {
                _logger.LogWarning($"Failed to download GTFS realtime trip data. Network interrupted during download: {ex.Message}");
                return;
            }            
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to download GTFS realtime trip data: {ex.Message}");
                return;
            }

            using IServiceScope scope = _serviceProvider.CreateScope();
            PassengerManagerContext context = scope.ServiceProvider.GetRequiredService<PassengerManagerContext>();

            try
            {
                await ProcessTripUpdates(feed, context);
                await ProcessAlerts(feed, context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to process GTFS realtime trip data.");
            }
        }       
    }
}
