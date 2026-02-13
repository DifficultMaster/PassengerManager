using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using PassengerManager.Server.Extensions;
using PassengerManager.Server.Models;
using PassengerManager.Shared.Protos;
using System.Security.Claims;

namespace PassengerManager.Server.Services
{
    [Authorize(Roles = "Driver")]
    public class DriverOpsService : PassengerManager.Shared.Protos.DriverOpsService.DriverOpsServiceBase
    {
        private readonly ILogger<DriverOpsService> _logger;
        private readonly PassengerManagerContext _context;

        public DriverOpsService(ILogger<DriverOpsService> logger, PassengerManagerContext context)
        {
            _logger = logger;
            _context = context;
        }

        public override async Task<SetShiftRouteResponse> SetShiftRoute(SetShiftRouteRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();           

            try
            {
                ClaimsPrincipal user = context.GetHttpContext().User;
                long shiftId = user.GetShiftId();

                // CASE: Failure - Missing or invalid shift ID
                if (shiftId <= 0)
                { 
                    return new SetShiftRouteResponse
                    {
                        Success = false,
                        Message = "Unauthorized: Invalid token state",
                        Code = DriverOpsResultCode.Unauthorized
                    };
                }

                Shared.Models.Shift? shift = await _context.Shifts.FindAsync(shiftId);

                // CASE : Failure - Shift not found
                if (shift == null || shift.EndTime != null)
                {
                    return new SetShiftRouteResponse
                    {
                        Success = false,
                        Message = "Shift is closed",
                        Code = DriverOpsResultCode.ShiftInactive
                    };
                }

                // CASE: Success - route already set to this shift
                if (shift.RouteId == request.RouteId)
                {
                    return new SetShiftRouteResponse
                    {
                        Success = true,
                        Message = "Route set successfully",
                        Code = DriverOpsResultCode.Success
                    };
                }

                // CASE: Success - route will now be set to this shift
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    shift.RouteId = request.RouteId;
                    shift.CurrentTripId = null;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return new SetShiftRouteResponse
                    {
                        Success = true,
                        Message = "Route set successfully",
                        Code = DriverOpsResultCode.Success
                    };
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DriverOpsService during SetShiftRoute");

                return new SetShiftRouteResponse
                {
                    Success = false,
                    Message = "Internal server error",
                    Code = DriverOpsResultCode.Unknown
                };
            }
        }
    }
}
