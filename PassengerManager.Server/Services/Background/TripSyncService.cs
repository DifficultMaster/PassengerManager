using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using PassengerManager.Server.Models;
using PassengerManager.Server.Protos.Static;
using PassengerManager.Server.Services.Static;
using PassengerManager.Shared.DTOs;
using PassengerManager.Shared.Models;
using StackExchange.Redis;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace PassengerManager.Server.Services.Background
{
    public class TripSyncService : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly GtfsScaleSettings _scaleSettings;
        private readonly ILogger<TripSyncService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConnectionMultiplexer _redis;
        private readonly TelemetryChannels _channels;

        private DateTimeOffset? _lastUpdated = null;
        private EntityTagHeaderValue? _lastEntityTag = null;

        public TripSyncService(
            IConfiguration configuration,
            GtfsScaleSettings scaleSettings, 
            ILogger<TripSyncService> logger, 
            IHttpClientFactory httpClientFactory,
            IConnectionMultiplexer redis,
            TelemetryChannels channels)
        {
            _configuration = configuration;
            _scaleSettings = scaleSettings;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _redis = redis;
            _channels = channels;
        }

        private async Task ProcessTripUpdatesAsync(FeedMessage feed, CancellationToken token)
        {
            List<FeedEntity> entities = feed.Entity.Where(e => e.TripUpdate != null).ToList();
            if (!entities.Any()) return;

            DateTime now = DateTime.UtcNow;
            StackExchange.Redis.IDatabase db = _redis.GetDatabase();

            foreach (FeedEntity entity in entities)
            {
                Protos.Static.TripUpdate gtfsTripUpdate = entity.TripUpdate;

                if (gtfsTripUpdate.Trip == null || string.IsNullOrEmpty(gtfsTripUpdate.Trip.TripId))
                    continue;

                int? delaySeconds = gtfsTripUpdate.HasDelay
                    ? gtfsTripUpdate.Delay
                    : gtfsTripUpdate.StopTimeUpdate.LastOrDefault(s => s.Arrival != null && s.Arrival.HasDelay)?.Arrival?.Delay
                      ?? gtfsTripUpdate.StopTimeUpdate.LastOrDefault(s => s.Departure != null && s.Departure.HasDelay)?.Departure?.Delay;

                TripUpdateDto dto = new TripUpdateDto
                {
                    TripId = gtfsTripUpdate.Trip.TripId,
                    VehicleId = string.IsNullOrEmpty(gtfsTripUpdate.Vehicle?.Id) ? null : gtfsTripUpdate.Vehicle.Id,
                    Timestamp = gtfsTripUpdate.HasTimestamp ? DateTimeOffset.FromUnixTimeSeconds((long)gtfsTripUpdate.Timestamp).UtcDateTime : now,
                    DelaySeconds = delaySeconds
                };

                try
                {
                    string json = JsonSerializer.Serialize(dto);
                    await db.StringSetAsync($"trip:{dto.TripId}", json, TimeSpan.FromSeconds(_scaleSettings.RedisTtlSeconds), flags: CommandFlags.FireAndForget);
                }
                catch (RedisConnectionException e)
                {
                    _logger.LogWarning($"Failed to connect to Redis: {e.Message}");
                }

                await _channels.TripChannel.Writer.WriteAsync(dto, token);
            }
        }

        private async Task ProcessAlertsAsync(FeedMessage feed, CancellationToken token)
        {
            List<FeedEntity> entities = feed.Entity.Where(e => e.Alert != null).ToList();
            if (!entities.Any()) return;

            StackExchange.Redis.IDatabase db = _redis.GetDatabase();

            foreach (FeedEntity entity in entities)
            {
                Alert gtfsAlert = entity.Alert;
                if (string.IsNullOrEmpty(entity.Id)) continue;

                EntitySelector? informed = gtfsAlert.InformedEntity.FirstOrDefault();
                TimeRange? activePeriod = gtfsAlert.ActivePeriod.FirstOrDefault();

                DateTime? startTime = activePeriod != null && activePeriod.HasStart ? DateTimeOffset.FromUnixTimeSeconds((long)activePeriod.Start).UtcDateTime : null;
                DateTime? endTime = activePeriod != null && activePeriod.HasEnd ? DateTimeOffset.FromUnixTimeSeconds((long)activePeriod.End).UtcDateTime : null;
                bool isActive = endTime == null || endTime > DateTime.UtcNow;

                ServiceAlertDto dto = new ServiceAlertDto
                {
                    AlertId = entity.Id,
                    AgencyId = informed?.AgencyId,
                    RouteId = informed?.RouteId,
                    StopId = informed?.StopId,
                    HeaderText = gtfsAlert.HeaderText?.Translation.FirstOrDefault()?.Text,
                    DescriptionText = gtfsAlert.DescriptionText?.Translation.FirstOrDefault()?.Text,
                    Cause = gtfsAlert.HasCause ? (int?)gtfsAlert.Cause : null,
                    Effect = gtfsAlert.HasEffect ? (int?)gtfsAlert.Effect : null,
                    StartTime = startTime,
                    EndTime = endTime,
                    IsActive = isActive
                };

                try
                {
                    string json = JsonSerializer.Serialize(dto);
                    await db.StringSetAsync($"alert:{dto.AlertId}", json, TimeSpan.FromMinutes(5),
                        flags: CommandFlags.FireAndForget);
                }
                catch (RedisConnectionException e)
                {
                    _logger.LogWarning($"Failed to connect to Redis: {e.Message}");
                }
                
                await _channels.AlertChannel.Writer.WriteAsync(dto, token);
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

            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(_configuration.GetValue<int>("HttpSettings:GtfsClient:TimeoutSeconds", 10) + 5));
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

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

            try
            {
                await ProcessTripUpdatesAsync(feed, token);
                await ProcessAlertsAsync(feed, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to process GTFS realtime trip data.");
            }
        }       
    }
}
