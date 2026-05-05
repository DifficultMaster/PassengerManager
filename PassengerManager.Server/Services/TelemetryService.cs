using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using PassengerManager.Server.Extensions;
using PassengerManager.Server.Services.Events;
using PassengerManager.Server.Services.Interfaces;
using PassengerManager.Server.Services.Security;
using PassengerManager.Shared.Protos;
using System.Security.Claims;

namespace PassengerManager.Server.Services
{
    [Authorize(Roles = "Hardware")]
    public class TelemetryService : PassengerManager.Shared.Protos.TelemetryService.TelemetryServiceBase
    {
        private readonly ILogger<AuthService> _logger;
        private readonly IMessageService _messageService;

        public TelemetryService(ILogger<AuthService> logger, IMessageService messageService)
        {
            _logger = logger;
            _messageService = messageService;
        }

        public override async Task<SendHeartbeatResponse> SendHeartbeat(SendHeartbeatRequest request, ServerCallContext context)
        {
            SendHeartbeatResponse response = new SendHeartbeatResponse
            {
                Success = false
            };

            try
            {
                ClaimsPrincipal user = context.GetHttpContext().User;

                await _messageService.PublishSafeAsync(
                    new TelemetryEvents.HeartbeatReceived(
                        VehicleId: user.FindFirst("VehicleId")?.Value ?? string.Empty,
                        Latitude: request.Latitude,
                        Longitude: request.Longitude,
                        Bearing: request.Bearing,
                        Speed: request.Speed,
                        Odometer: request.Odometer,
                        AgencyId: user.FindFirst("AgencyId")?.Value),
                    "Telemetry.HeartbeatReceived",
                    context.CancellationToken
                    );

                response.Success = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TelemetryService during SendHeartbeat");
                response.Success = false;
            }

            return response;
        }

        public override async Task<SendStatusResponse> SendStatus(SendStatusRequest request, ServerCallContext context)
        {
            SendStatusResponse response = new SendStatusResponse
            {
                Success = false
            };

            try
            {
                ClaimsPrincipal user = context.GetHttpContext().User;

                await _messageService.PublishSafeAsync(
                    new TelemetryEvents.StatusReceived(
                        VehicleId: user.FindFirst("VehicleId")?.Value ?? string.Empty,
                        CurrentStatus: request.CurrentStatus,
                        CurrentStopSequence: request.CurrentStopSequence,
                        CongestionLevel: request.CongestionLevel,
                        OccupancyStatus: request.OccupancyStatus,
                        AgencyId: user.FindFirst("AgencyId")?.Value),
                    "Telemetry.StatusReceived",
                    context.CancellationToken
                    );

                response.Success = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TelemetryService during SendStatus");
                response.Success = false;
            }

            return response;
        }
    }
}
