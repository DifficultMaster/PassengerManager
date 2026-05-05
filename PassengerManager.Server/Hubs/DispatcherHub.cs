using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace PassengerManager.Server.Hubs
{
    [Authorize(Roles = "Dispatcher")]
    public class DispatcherHub : Hub
    {
        private string? TryGetAgencyId()
        {
            ClaimsPrincipal? user = Context.User;
            return user == null ? null : (user.FindFirst("AgencyId")?.Value ?? string.Empty);
        }

        private string? TryGetUserId()
        {
            ClaimsPrincipal? user = Context.User;
            return user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value;
        }

        public override async Task OnConnectedAsync()
        {
            ClaimsPrincipal? user = Context.User;

            if (user == null)
            {
                Context.Abort();
                return;
            }

            string agencyId = user.FindFirst("AgencyId")?.Value ?? string.Empty;

            if (string.IsNullOrEmpty(agencyId))
            {
                Context.Abort();
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"Agency_{agencyId}_Dispatchers");
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Join a call-specific group for coordinated communication during active calls.
        /// </summary>
        public async Task JoinCallGroup(string callId)
        {
            string? agencyId = TryGetAgencyId();
            if (agencyId == null)
            {
                Context.Abort();
                return;
            }

            string callGroupName = $"Agency_{agencyId}_Call_{callId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, callGroupName);
        }

        /// <summary>
        /// Leave a call-specific group when the call ends.
        /// </summary>
        public async Task LeaveCallGroup(string callId)
        {
            string? agencyId = TryGetAgencyId();
            if (agencyId == null)
            {
                return;
            }

            string callGroupName = $"Agency_{agencyId}_Call_{callId}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, callGroupName);
        }
    }
}
