using System.Security;
using System.Security.Claims;

namespace PassengerManager.Server.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static int GetUserId(this ClaimsPrincipal user)
        {
            Claim? claim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub");
            return claim != null && int.TryParse(claim.Value, out int userId) ? userId : 0;
        }

        public static long GetShiftId(this ClaimsPrincipal user)
        {
            Claim? claim = user.FindFirst("ShiftId");

            if (claim == null)
            {
                throw new SecurityException("No active shift found in token");
            }

            return long.TryParse(claim.Value, out long shiftId) ? shiftId : 0;
        }

        public static string GetVehicleId(this ClaimsPrincipal user)
        {
            return user.FindFirst("VehicleId")?.Value ?? string.Empty;
        }

        public static string GetAgencyId(this ClaimsPrincipal user)
        {
            return user.FindFirst("AgencyId")?.Value ?? string.Empty;
        }
    }
}
