namespace PassengerManager.Server.Services.Interfaces
{
    public interface INotificationService
    {
        Task AlertDispatchersByAgency(string agencyId, string alertId, string routeId, string type);

        Task AlertDriversByRoute(string agencyId, string routeId, string message);

        Task AlertDriver(string driverUserId, string message, bool isUrgent);
    }
}
