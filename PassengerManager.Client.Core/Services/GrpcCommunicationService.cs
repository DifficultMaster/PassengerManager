using Grpc.Core;
using PassengerManager.Client.Core.Services.Interfaces;
using PassengerManager.Shared.Protos;

namespace PassengerManager.Client.Core.Services
{
    public class GrpcCommunicationService : ICommunicationService
    {
        private readonly CommunicationService.CommunicationServiceClient _client;

        public GrpcCommunicationService(CommunicationService.CommunicationServiceClient client)
        {
            _client = client;
        }

        public async Task<InitiateCallResponse> InitiateCallAsync(InitiateCallRequest request)
        {
            try
            {
                return await _client.InitiateCallAsync(request);
            }
            catch (RpcException ex)
            {
                return new InitiateCallResponse
                {
                    Success = false,
                    Message = $"Network error: {ex.Status.Detail}"
                };
            }
            catch
            {
                return new InitiateCallResponse
                {
                    Success = false,
                    Message = "Unhandled local exception"
                };
            }
        }

        public async Task<EndCallResponse> EndCallAsync(EndCallRequest request)
        {
            try
            {
                return await _client.EndCallAsync(request);
            }
            catch (RpcException ex)
            {
                return new EndCallResponse
                {
                    Success = false,
                    Message = $"Network error: {ex.Status.Detail}"
                };
            }
            catch
            {
                return new EndCallResponse
                {
                    Success = false,
                    Message = "Unhandled local exception"
                };
            }
        }
    }
}
