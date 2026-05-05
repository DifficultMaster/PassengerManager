using Grpc.Core;
using Microsoft.Extensions.Logging;
using PassengerManager.Client.Core.Services.Interfaces;
using PassengerManager.Shared.Protos;

namespace PassengerManager.Client.Core.Services
{
    /// <summary>
    /// gRPC implementation of telemetry service for sending vehicle data to the server.
    /// </summary>
    public class GrpcTelemetryService : ITelemetryService
    {
        private readonly TelemetryService.TelemetryServiceClient _client;
        private readonly ILogger<GrpcTelemetryService> _logger;

        public GrpcTelemetryService(TelemetryService.TelemetryServiceClient client, ILogger<GrpcTelemetryService> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<SendHeartbeatResponse> SendHeartbeatAsync(SendHeartbeatRequest request)
        {
            try
            {
                return await _client.SendHeartbeatAsync(request);
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RPC error sending heartbeat: {Status}", ex.Status.Detail);
                return new SendHeartbeatResponse { Success = false };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending heartbeat");
                return new SendHeartbeatResponse { Success = false };
            }
        }

        public async Task<SendStatusResponse> SendStatusAsync(SendStatusRequest request)
        {
            try
            {
                return await _client.SendStatusAsync(request);
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RPC error sending status: {Status}", ex.Status.Detail);
                return new SendStatusResponse { Success = false };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending status");
                return new SendStatusResponse { Success = false };
            }
        }
    }
}
