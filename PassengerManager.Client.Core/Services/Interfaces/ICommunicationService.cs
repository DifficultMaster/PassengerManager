using PassengerManager.Shared.Protos;

namespace PassengerManager.Client.Core.Services.Interfaces
{
    public interface ICommunicationService
    {
        Task<InitiateCallResponse> InitiateCallAsync(InitiateCallRequest request);

        Task<EndCallResponse> EndCallAsync(EndCallRequest request);
    }
}
