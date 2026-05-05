using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace PassengerManager.Server.Hubs
{
    [Authorize(Roles = "Driver")]
    public class DriverHub : Hub
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
            string? agencyId = TryGetAgencyId();

            if (agencyId == null)
            {
                Context.Abort();
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"Agency_{agencyId}_Drivers");
            await base.OnConnectedAsync();
        }

        public async Task SwitchRouteGroup(string? oldRouteId, string? newRouteId)
        {
            string? agencyId = TryGetAgencyId();

            if (agencyId == null)
            {
                Context.Abort();
                return;
            }

            if (!string.IsNullOrEmpty(oldRouteId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Agency_{agencyId}_Route_{oldRouteId}_Drivers");
            }        

            if (!string.IsNullOrEmpty(newRouteId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Agency_{agencyId}_Route_{newRouteId}_Drivers");
            }
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
