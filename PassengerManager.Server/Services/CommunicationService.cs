using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using PassengerManager.Server.Extensions;
using PassengerManager.Server.Services.Events;
using PassengerManager.Server.Services.Interfaces;
using PassengerManager.Shared.Protos;
using System.Security.Claims;

namespace PassengerManager.Server.Services
{
    [Authorize]
    public class CommunicationService : PassengerManager.Shared.Protos.CommunicationService.CommunicationServiceBase
    {
        private readonly ILogger<CommunicationService> _logger;
        private readonly IMessageService _messageService;
        private readonly DispatcherStateTrackerService _dispatcherStateTracker;
        private readonly INotificationService _notificationService;

        public CommunicationService(
            ILogger<CommunicationService> logger, 
            IMessageService messageService, 
            DispatcherStateTrackerService dispatcherStateTracker,
            INotificationService notificationService)
        {
            _logger = logger;
            _messageService = messageService;
            _dispatcherStateTracker = dispatcherStateTracker;
            _notificationService = notificationService;
        }

        public override async Task<InitiateCallResponse> InitiateCall(InitiateCallRequest request, ServerCallContext context)
        {
            ClaimsPrincipal user = context.GetHttpContext().User;
            string callerRole = user.FindFirst(ClaimTypes.Role)?.Value ?? "";
            string agencyId = user.GetAgencyId();
            int callerUserId = user.GetUserId();
            string callId = Guid.NewGuid().ToString();

            InitiateCallResponse response = new InitiateCallResponse
            {
                Success = false,
                Message = "Internal server error"
            };

            string? targetDispatcherId = null;
            string? failureReason = null;

            try
            {
                // Validate agency ID
                if (string.IsNullOrEmpty(agencyId))
                {
                    response.Success = false;
                    response.Message = "Unauthorized: No agency context";
                    failureReason = "Missing agency ID";
                    return response;
                }

                // CASE: Driver or Hardware-initiated call
                if (callerRole is "Hardware" or "Driver")
                {
                    string vehicleId = user.GetVehicleId();

                    if (string.IsNullOrEmpty(vehicleId))
                    {
                        response.Success = false;
                        response.Message = "Unauthorized: No vehicle context";
                        failureReason = "Missing vehicle ID";
                        return response;
                    }

                    // Emergency call path
                    if (request.CallType == InitiateCallRequest.Types.CallType.Emergency)
                    {
                        _logger.LogWarning($"SOS CALL INITIATED BY VEHICLE {vehicleId} (Agency: {agencyId})");

                        // Notify all dispatchers in the agency of the emergency
                        await _notificationService.NotifyDispatchersOfEmergencyCall(agencyId, callId, vehicleId);

                        response.Success = true;
                        response.Message = "Emergency call routed to all available dispatchers";
                        response.AssignedTargetId = "EMERGENCY_BROADCAST";
                        response.CallId = callId;

                        await _messageService.PublishSafeAsync(
                            new CommunicationEvents.CallInitiated(
                                CallId: callId,
                                CallerUserId: callerUserId,
                                CallerVehicleId: vehicleId,
                                CallerRole: callerRole,
                                TargetDispatcherId: null,
                                CallType: "Emergency",
                                AgencyId: agencyId,
                                InitiatedAtUtc: DateTime.UtcNow),
                            "Communication.EmergencyCallInitiated",
                            context.CancellationToken);
                    }
                    // Standard call path
                    else
                    {
                        // Find least busy dispatcher within the same agency
                        targetDispatcherId = await _dispatcherStateTracker.GetLeastBusyDispatcherIdAsync(agencyId);

                        if (string.IsNullOrEmpty(targetDispatcherId))
                        {
                            response.Success = false;
                            response.Message = "No available dispatchers";
                            failureReason = "No dispatchers online in agency";
                            return response;
                        }

                        // Increment the dispatcher's load
                        await _dispatcherStateTracker.IncrementDispatcherLoadAsync(targetDispatcherId, agencyId);

                        // Notify the assigned dispatcher
                        await _notificationService.NotifyDispatcherOfIncomingCall(agencyId, targetDispatcherId, callId, vehicleId, "Standard");

                        // Notify the driver of assignment
                        string driverUserId = callerUserId.ToString();
                        await _notificationService.NotifyDriverOfAssignedDispatcher(driverUserId, targetDispatcherId, callId);

                        response.Success = true;
                        response.Message = "Call routed to dispatcher";
                        response.AssignedTargetId = targetDispatcherId;
                        response.CallId = callId;

                        await _messageService.PublishSafeAsync(
                            new CommunicationEvents.CallInitiated(
                                CallId: callId,
                                CallerUserId: callerUserId,
                                CallerVehicleId: vehicleId,
                                CallerRole: callerRole,
                                TargetDispatcherId: targetDispatcherId,
                                CallType: "Standard",
                                AgencyId: agencyId,
                                InitiatedAtUtc: DateTime.UtcNow),
                            "Communication.CallInitiated",
                            context.CancellationToken);

                        await _messageService.PublishSafeAsync(
                            new CommunicationEvents.CallAssigned(
                                CallId: callId,
                                AssignedDispatcherId: targetDispatcherId,
                                VehicleId: vehicleId,
                                AgencyId: agencyId,
                                AssignedAtUtc: DateTime.UtcNow),
                            "Communication.CallAssigned",
                            context.CancellationToken);
                    }
                }
                // CASE: Dispatcher-initiated call (direct call to a specific target)
                else if (callerRole == "Dispatcher")
                {
                    string targetId = request.TargetId;

                    if (string.IsNullOrEmpty(targetId))
                    {
                        response.Success = false;
                        response.Message = "Invalid target ID";
                        failureReason = "Target ID not provided";
                        return response;
                    }

                    response.Success = true;
                    response.Message = "Call initiated";
                    response.AssignedTargetId = targetId;
                    response.CallId = callId;

                    await _messageService.PublishSafeAsync(
                        new CommunicationEvents.CallInitiated(
                            CallId: callId,
                            CallerUserId: callerUserId,
                            CallerVehicleId: null,
                            CallerRole: callerRole,
                            TargetDispatcherId: targetId,
                            CallType: "Standard",
                            AgencyId: agencyId,
                            InitiatedAtUtc: DateTime.UtcNow),
                        "Communication.CallInitiated",
                        context.CancellationToken);
                }
                else
                {
                    response.Success = false;
                    response.Message = "Unauthorized: Invalid role for call initiation";
                    failureReason = $"Invalid role: {callerRole}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CommunicationService during InitiateCall for agency {AgencyId}", agencyId);

                response.Success = false;
                response.Message = "Internal server error";
                failureReason = ex.Message;
            }
            finally
            {
                // Publish failure event if call was not successful
                if (!response.Success && !string.IsNullOrEmpty(failureReason))
                {
                    await _messageService.PublishSafeAsync(
                        new CommunicationEvents.CallInitiated(
                            CallId: callId,
                            CallerUserId: callerUserId,
                            CallerVehicleId: user.GetVehicleId(),
                            CallerRole: callerRole,
                            TargetDispatcherId: targetDispatcherId,
                            CallType: request.CallType.ToString(),
                            AgencyId: agencyId,
                            InitiatedAtUtc: DateTime.UtcNow,
                            FailureReason: failureReason),
                        "Communication.CallFailed",
                        context.CancellationToken);
                }
            }

            return response;
        }

        public override async Task<EndCallResponse> EndCall(EndCallRequest request, ServerCallContext context)
        {
            ClaimsPrincipal user = context.GetHttpContext().User;
            string callerRole = user.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            string agencyId = user.GetAgencyId();

            EndCallResponse response = new EndCallResponse
            {
                Success = false,
                Message = "Internal server error"
            };

            try
            {
                if (string.IsNullOrEmpty(agencyId))
                {
                    response.Success = false;
                    response.Message = "Unauthorized: No agency context";
                    return response;
                }

                if (string.IsNullOrWhiteSpace(request.CallId))
                {
                    response.Success = false;
                    response.Message = "Invalid call ID";
                    return response;
                }

                bool isEmergency = request.CallType == InitiateCallRequest.Types.CallType.Emergency;
                bool canEndCall = callerRole == "Dispatcher" || (!isEmergency && callerRole == "Driver");

                if (!canEndCall)
                {
                    response.Success = false;
                    response.Message = "Only dispatchers can end emergency calls";
                    return response;
                }

                response.Success = true;
                response.Message = "Call ended";

                await _messageService.PublishSafeAsync(
                    new CommunicationEvents.CallEnded(
                        CallId: request.CallId,
                        AgencyId: agencyId,
                        EndedAtUtc: DateTime.UtcNow),
                    "Communication.CallEnded",
                    context.CancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CommunicationService during EndCall for agency {AgencyId}", agencyId);

                response.Success = false;
                response.Message = "Internal server error";
            }

            return response;
        }
    }
}
