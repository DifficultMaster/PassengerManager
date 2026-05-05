using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PassengerManager.Server.Hubs
{
    [Authorize]
    public class WebRtcHub : Hub
    {
        public async Task SendOffer(string targetId, string sdpOffer)
        {
            await Clients.User(targetId).SendAsync("ReceiveOffer", Context.UserIdentifier, sdpOffer);
        }

        public async Task SendAnswer(string targetId, string sdpAnswer)
        {
            await Clients.User(targetId).SendAsync("ReceiveOffer", Context.UserIdentifier, sdpAnswer);
        }

        public async Task SendIceCandidate(string targetId, string candidateJson)
        {
            await Clients.User(targetId).SendAsync("ReceiveIceCandidate", Context.UserIdentifier, candidateJson);
        }
    }
}
