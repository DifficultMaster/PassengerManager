using Microsoft.AspNetCore.SignalR;
using PassengerManager.Server.Hubs;
using PassengerManager.Server.Services.Interfaces;

namespace PassengerManager.Server.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IHubContext<DispatcherHub> _dispatcherHubContext;
        private readonly IHubContext<DriverHub> _driverHubContext;

        public NotificationService(
            IHubContext<DispatcherHub> dispatcherHubContext, 
            IHubContext<DriverHub> driverHubContext)
        {
            _dispatcherHubContext = dispatcherHubContext;
            _driverHubContext = driverHubContext;
        }

        public async Task AlertDispatchersByAgency(string agencyId, string alertId, string routeId, string type)
        {
            string groupName = $"Agency_{agencyId}_Dispatchers";

            await _dispatcherHubContext.Clients.Group(groupName)
                .SendAsync("ReceiveServiceAlert", alertId, routeId, type);
        }

        public async Task AlertDriversByRoute(string agencyId, string routeId, string message)
        {
            string groupName = $"Agency_{agencyId}_Route_{routeId}_Drivers";

            await _driverHubContext.Clients.Group(groupName)
                .SendAsync("ReceiveRouteAlert", message);
        }

        public async Task AlertDriver(string driverUserId, string message, bool isUrgent)
        {
            await _driverHubContext.Clients.User(driverUserId)
                .SendAsync("ReceiveServiceAlert", message, isUrgent);
        }

        /// <summary>
        /// Notify a specific dispatcher of an incoming call within their agency.
        /// </summary>
        public async Task NotifyDispatcherOfIncomingCall(string agencyId, string dispatcherId, string callId, string vehicleId, string callType)
        {
            string groupName = $"Agency_{agencyId}_Dispatchers";

            await _dispatcherHubContext.Clients.User(dispatcherId)
                .SendAsync("ReceiveIncomingCall", callId, vehicleId, callType);
        }

        /// <summary>
        /// Notify dispatchers in an agency of an incoming emergency call.
        /// </summary>
        public async Task NotifyDispatchersOfEmergencyCall(string agencyId, string callId, string vehicleId)
        {
            string groupName = $"Agency_{agencyId}_Dispatchers";

            await _dispatcherHubContext.Clients.Group(groupName)
                .SendAsync("ReceiveEmergencyCall", callId, vehicleId);
        }

        /// <summary>
        /// Notify a driver about the dispatcher assigned to their call.
        /// </summary>
        public async Task NotifyDriverOfAssignedDispatcher(string vehicleUserId, string assignedDispatcherId, string callId)
        {
            await _driverHubContext.Clients.User(vehicleUserId)
                .SendAsync("DispatcherAssigned", assignedDispatcherId, callId);
        }
    }
}
