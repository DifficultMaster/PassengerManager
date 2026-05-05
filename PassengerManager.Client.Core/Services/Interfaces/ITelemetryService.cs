using PassengerManager.Shared.Protos;

namespace PassengerManager.Client.Core.Services.Interfaces
{
    /// <summary>
    /// Service for sending telemetry data (heartbeat, status) to the server.
    /// </summary>
    public interface ITelemetryService
    {
        /// <summary>
        /// Sends a heartbeat with vehicle location and motion data.
        /// </summary>
        Task<SendHeartbeatResponse> SendHeartbeatAsync(SendHeartbeatRequest request);

        /// <summary>
        /// Sends vehicle status update (transit status, occupancy, etc).
        /// </summary>
        Task<SendStatusResponse> SendStatusAsync(SendStatusRequest request);
    }
}
