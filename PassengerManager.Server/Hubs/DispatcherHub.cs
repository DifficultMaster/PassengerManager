using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace PassengerManager.Server.Hubs
{
    [Authorize(Roles = "Dispatcher")]
    public class DispatcherHub : Hub
    {         
        public override async Task OnConnectedAsync()
        {
            ClaimsPrincipal? user = Context.User;

            if (user == null)
            {
                Context.Abort();
                return;
            }

            string agencyId = user.FindFirst("AgencyId")?.Value ?? string.Empty;

            await Groups.AddToGroupAsync(Context.ConnectionId, $"Agency_{agencyId}_Dispatchers");
            await base.OnConnectedAsync();
        }
    }
}
