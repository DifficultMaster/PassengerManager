using System.Text.Json;
using MassTransit;
using PassengerManager.Server.Services.Background;
using PassengerManager.Server.Services.Events;
using PassengerManager.Shared.DTOs;
using StackExchange.Redis;

namespace PassengerManager.Server.Services.Consumers
{
    public class HeartbeatConsumer : IConsumer<TelemetryEvents.HeartbeatReceived>
    {
        private readonly TelemetryChannels _channels;
        private readonly IConnectionMultiplexer _redis;
        private readonly GtfsScaleSettings _scaleSettings;
        private readonly ILogger<HeartbeatConsumer> _logger;

        public HeartbeatConsumer(TelemetryChannels channels, IConnectionMultiplexer redis, 
            GtfsScaleSettings scaleSettings, ILogger<HeartbeatConsumer> logger)
        {
            _channels = channels;
            _redis = redis;
            _scaleSettings = scaleSettings;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<TelemetryEvents.HeartbeatReceived> context)
        {
            var heartbeat = context.Message;

            var dto = new VehiclePositionDto()
            {
                VehicleId = heartbeat.VehicleId,
                Latitude = heartbeat.Latitude,
                Longitude = heartbeat.Longitude,
                Bearing = (float)heartbeat.Bearing!,
                Speed = heartbeat.Speed,
                Odometer = heartbeat.Odometer,
                Timestamp = DateTime.UtcNow,
                RouteId = heartbeat.RouteId,
                TripId = heartbeat.TripId
            };

            try
            {
                StackExchange.Redis.IDatabase db = _redis.GetDatabase();
                string json = JsonSerializer.Serialize(dto);
                await db.StringSetAsync(
                    $"vehicle:{dto.VehicleId}",
                    json,
                    TimeSpan.FromSeconds(_scaleSettings.RedisTtlSeconds),
                    flags: CommandFlags.FireAndForget);
            }
            catch (RedisConnectionException e)
            {
                _logger.LogWarning($"Failed to connect to Redis: {e.Message}");
            }
            finally
            {
                await _channels.VehicleChannel.Writer.WriteAsync(dto, context.CancellationToken);
            }
        }
    }
}
