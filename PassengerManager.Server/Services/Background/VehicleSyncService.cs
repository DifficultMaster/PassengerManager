using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
    public class VehicleSyncService : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly GtfsScaleSettings _scaleSettings;
        private readonly ILogger<VehicleSyncService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConnectionMultiplexer _redis;
        private readonly TelemetryChannels _channels;

        private DateTimeOffset? _lastUpdated = null;
        private EntityTagHeaderValue? _lastEntityTag = null;

        public VehicleSyncService(
            IConfiguration configuration, 
            GtfsScaleSettings scaleSettings,
            ILogger<VehicleSyncService> logger, 
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

        private async Task ProcessFeedAsync(FeedMessage feed, CancellationToken token)
        {
            List<FeedEntity> entities = feed.Entity.Where(e => e.Vehicle != null).ToList();
            if (!entities.Any()) return;

            DateTime now = DateTime.UtcNow;
            StackExchange.Redis.IDatabase db = _redis.GetDatabase();

            foreach (FeedEntity entity in entities)
            {
                Protos.Static.VehiclePosition gtfsVehicle = entity.Vehicle;

                if (gtfsVehicle.Vehicle == null || string.IsNullOrEmpty(gtfsVehicle.Vehicle.Id))
                    continue;

                VehiclePositionDto dto = new VehiclePositionDto
                {
                    VehicleId = gtfsVehicle.Vehicle.Id,
                    LicensePlate = gtfsVehicle.Vehicle.HasLicensePlate ? gtfsVehicle.Vehicle.LicensePlate : null,
                    RouteId = gtfsVehicle.Trip?.RouteId,
                    TripId = gtfsVehicle.Trip?.TripId,

                    Latitude = gtfsVehicle.Position?.Latitude ?? 0,
                    Longitude = gtfsVehicle.Position?.Longitude ?? 0,

                    Bearing = gtfsVehicle.Position?.HasBearing == true ? gtfsVehicle.Position.Bearing : null,
                    Speed = gtfsVehicle.Position?.HasSpeed == true ? gtfsVehicle.Position.Speed : null,
                    Odometer = gtfsVehicle.Position?.HasOdometer == true ? gtfsVehicle.Position.Odometer : null,

                    CurrentStatus = gtfsVehicle.HasCurrentStatus ? (int)gtfsVehicle.CurrentStatus : null,
                    StopId = string.IsNullOrWhiteSpace(gtfsVehicle.StopId) ? null : gtfsVehicle.StopId,
                    CurrentStopSequence = gtfsVehicle.HasCurrentStopSequence ? (int)gtfsVehicle.CurrentStopSequence : null,
                    CongestionLevel = gtfsVehicle.HasCongestionLevel ? (int)gtfsVehicle.CongestionLevel : null,
                    OccupancyStatus = gtfsVehicle.HasOccupancyStatus ? (int)gtfsVehicle.OccupancyStatus : null,

                    Timestamp = gtfsVehicle.HasTimestamp ? DateTimeOffset.FromUnixTimeSeconds((long)gtfsVehicle.Timestamp).UtcDateTime : now
                };

                try
                {
                    string json = JsonSerializer.Serialize(dto);
                    await db.StringSetAsync($"vehicle:{dto.VehicleId}", json,
                        TimeSpan.FromSeconds(_scaleSettings.RedisTtlSeconds), flags: CommandFlags.FireAndForget);
                }
                catch (RedisConnectionException e)
                {
                    _logger.LogWarning($"Failed to connect to Redis: {e.Message}");
                }
                
                await _channels.VehicleChannel.Writer.WriteAsync(dto, token);
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

            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(_configuration.GetValue<int>("HttpSettings:GtfsClient:TimeoutSeconds", 10) + 5));
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

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

            try
            {
                await ProcessFeedAsync(feed, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to process GTFS realtime vehicle data.");
            }
        }
    }
}
