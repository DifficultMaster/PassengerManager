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
    }
}
