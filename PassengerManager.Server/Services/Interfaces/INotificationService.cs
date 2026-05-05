namespace PassengerManager.Server.Services.Interfaces
{
    public interface INotificationService
    {
        Task AlertDispatchersByAgency(string agencyId, string alertId, string routeId, string type);

        Task AlertDriversByRoute(string agencyId, string routeId, string message);

        Task AlertDriver(string driverUserId, string message, bool isUrgent);

        /// <summary>
        /// Notify a specific dispatcher of an incoming call within their agency.
        /// </summary>
        Task NotifyDispatcherOfIncomingCall(string agencyId, string dispatcherId, string callId, string vehicleId, string callType);

        /// <summary>
        /// Notify dispatchers in an agency of an incoming emergency call.
        /// </summary>
        Task NotifyDispatchersOfEmergencyCall(string agencyId, string callId, string vehicleId);

        /// <summary>
        /// Notify a driver about the dispatcher assigned to their call.
        /// </summary>
        Task NotifyDriverOfAssignedDispatcher(string vehicleUserId, string assignedDispatcherId, string callId);
    }
}
