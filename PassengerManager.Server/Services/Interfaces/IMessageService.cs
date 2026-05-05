namespace PassengerManager.Server.Services.Interfaces
{
    public interface IMessageService
    {
        Task PublishSafeAsync<TEvent>(TEvent message, string eventName, CancellationToken cancellationToken = default)
            where TEvent : class;
    }
}
