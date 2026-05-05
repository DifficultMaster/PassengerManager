using MassTransit;
using PassengerManager.Server.Services.Interfaces;

namespace PassengerManager.Server.Services
{
    /// <summary>
    /// Simple in-memory event publisher.
    /// In a production system, this would be replaced with MassTransit, RabbitMQ, or similar.
    /// For now, events are logged for observability.
    /// </summary>
    public class MessageService : IMessageService
    {
        private readonly IPublishEndpoint _publisher;
        private readonly ILogger<MessageService> _logger;

        public MessageService(IPublishEndpoint publisher, ILogger<MessageService> logger)
        {
            _publisher = publisher;
            _logger = logger;
        }

        public async Task PublishSafeAsync<TEvent>(TEvent message, string eventName, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            try
            {
                await _publisher.Publish(message, cancellationToken);
                _logger.LogInformation("Event published: {EventName}", eventName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish {EventName}", eventName);
            }
        }
    }
}
