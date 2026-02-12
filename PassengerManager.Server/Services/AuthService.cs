using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using PassengerManager.Server.Models;
using PassengerManager.Server.Services.Security;
using PassengerManager.Server.Services.Static;
using PassengerManager.Shared.Models;
using PassengerManager.Shared.Protos;
using System;
using static PassengerManager.Server.Services.Static.AuthDefaults;

namespace PassengerManager.Server.Services
{
    public class AuthService : PassengerManager.Shared.Protos.AuthService.AuthServiceBase
    {
        private readonly ILogger<AuthService> _logger;
        private readonly PassengerManagerContext _context;

        public AuthService(ILogger<AuthService> logger, PassengerManagerContext context)
        {
            _logger = logger;
            _context = context;
        }

        private static void ResetLockout(Shared.Models.User user)
        {
            user.FailedLoginAttempts = 0;
            user.IsLockedOut = false;
            user.LockoutEnd = null;
        }

        private async Task<List<Shared.Models.PasswordHistory>> GetRecentPasswordHistory(int userId, bool isStaff)
        {
            int recentPasswordHistoryCount = 0;
            if (isStaff)
            {
                recentPasswordHistoryCount = AuthDefaults.Staff.RecentPasswordHistoryCount;
            }
            else
            {
                recentPasswordHistoryCount = AuthDefaults.Terminal.RecentPasswordHistoryCount;
            }

            return await _context.PasswordHistories
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.CreatedAt)
                .Take(recentPasswordHistoryCount)
                .ToListAsync();
        }

        private static bool IsPasswordReused(
            Shared.Models.User user, string newHashedPassword, List<Shared.Models.PasswordHistory> history)
        {
            return user.PasswordHash == newHashedPassword
                || history.Any(h => h.PasswordHash == newHashedPassword);
        }

        private void IncrementFailedAttempts(Shared.Models.User user, bool isStaff)
        {
            user.FailedLoginAttempts = (user.FailedLoginAttempts ?? 0) + 1;

            int maxFailedAttempts = 0;
            if (isStaff)
            {
                maxFailedAttempts = AuthDefaults.Staff.MaxFailedAttempts;
            }
            else
            {
                maxFailedAttempts = AuthDefaults.Terminal.MaxFailedAttempts;
            }

            if (user.FailedLoginAttempts >= maxFailedAttempts)
            {
                user.IsLockedOut = true;
                user.LockoutEnd = DateTime.UtcNow.AddSeconds(AuthDefaults.Staff.LockoutDurationSeconds);
            }
        }

        public override async Task<StaffLoginResponse> StaffLogin(StaffLoginRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var (response, audit) = await ProcessStaffLogin(request, context);

                _context.LoginAudits.Add(audit);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return response;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error in AuthService during StaffLogin");

                return new StaffLoginResponse
                {
                    Success = false,
                    Message = "An error occurred during login. Please try again later",
                    Code = AuthResultCode.Unknown
                };  
            }
        }

        private async Task<(StaffLoginResponse Response, Shared.Models.LoginAudit Audit)> ProcessStaffLogin(
            StaffLoginRequest request, ServerCallContext context)
        {
            Shared.Models.LoginAudit audit = new Shared.Models.LoginAudit
            {
                UsernameAttempted = request.Username,
                AttemptTime = DateTime.UtcNow,
                IpAddress = context.Peer,
                UserAgent = "Desktop Client",
                IsSuccess = false,
            };

            Shared.Models.User? user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            // CASE: Failure - Invalid User ID
            if (user == null)
            {
                return (new StaffLoginResponse
                {
                    Success = false,
                    Message = "Incorrect username",
                    Code = AuthResultCode.InvalidLogin
                }, audit);
            }

            audit.UserId = user.Id;

            // CASE: Failure - Wrong UI
            if (user.Role == null || user.Role.RoleName.Equals("Driver", StringComparison.OrdinalIgnoreCase))
            {
                return (new StaffLoginResponse
                {
                    Success = false,
                    Message = user.Role == null
                        ? "User has no assigned role"
                        : "Drivers must use terminal mode to log in",
                    Code = AuthResultCode.InvalidMode
                }, audit);
            }

            // CASE: Failure - Account lockout
            if (user.IsLockedOut == true && user.LockoutEnd > DateTime.UtcNow)
            {
                double remaining = Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalSeconds);
                return (new StaffLoginResponse
                {
                    Success = false,
                    Message = $"Account is locked. Try again in {remaining} second(s)",
                    Code = AuthResultCode.AccountLockout
                }, audit);
            }            

            // CASE: Failure - Incorrect password
            if (!PasswordHandler.VerifyPassword(request.Password, user.PasswordHash))
            {
                IncrementFailedAttempts(user, true);
                return (new StaffLoginResponse
                {
                    Success = false,
                    Message = "Incorrect password",
                    Code = AuthResultCode.InvalidPassword
                }, audit);
            }

            // CASE: Success
            ResetLockout(user);
            user.LastLogin = DateTime.UtcNow;
            audit.IsSuccess = true;
            
            return (new StaffLoginResponse
            {
                Success = true,
                Message = "Login successful",
                Token = GenerateJwtToken(user),
                FullName = user.FullName ?? string.Empty,
                RoleName = user.Role.RoleName,
                AccessLevel = user.Role.AccessLevel,
                DefaultWindow = user.Role.DefaultWindow ?? string.Empty,
                Code = AuthResultCode.Success
            }, audit);
        }

        public override async Task<DriverLoginResponse> DriverLogin(DriverLoginRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var (response, audit) = await ProcessDriverLogin(request);

                _context.LoginAudits.Add(audit);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return response;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error in AuthService during DriverLogin");

                return new DriverLoginResponse
                {
                    Success = false,
                    Message = "An error occurred during login. Please try again later",
                    Code = AuthResultCode.Unknown
                };
            }
        }

        private async Task<(DriverLoginResponse Response, Shared.Models.LoginAudit Audit)> ProcessDriverLogin(
            DriverLoginRequest request, ServerCallContext context)
        { 
            Shared.Models.LoginAudit audit = new Shared.Models.LoginAudit
            {
                UsernameAttempted = request.UserId,
                AttemptTime = DateTime.UtcNow,
                IpAddress = context.Peer,
                UserAgent = "Terminal Client",
                IsSuccess = false,
            };

            // CASE: Failure - Invalid User ID format
            if (!int.TryParse(request.UserId, out int driverId))
            {
                return (new DriverLoginResponse
                {
                    Success = false,
                    Message = "Invalid driver ID format",
                    Code = AuthResultCode.InvalidLoginFormat
                }, audit);
            }

            Shared.Models.User? user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == driverId);

            // CASE: Failure - Invalid User ID
            if (user == null)
            {
                return (new DriverLoginResponse
                {
                    Success = false,
                    Message = $"Driver {request.UserId} not found",
                    Code = AuthResultCode.InvalidLogin
                }, audit);
            }

            // CASE: Failure - Wrong UI
            if (user.Role == null || !user.Role.RoleName.Equals("Driver", StringComparison.OrdinalIgnoreCase))
            {
                return (new DriverLoginResponse
                {
                    Success = false,
                    Message = user.Role == null
                        ? "User has no assigned role"
                        : "Staff must use desktop mode to log in",
                    Code = AuthResultCode.InvalidMode
                }, audit);
            }

            Shared.Models.Vehicle? vehicle = await _context.Vehicles
                .FindAsync(request.VehicleId);

            // CASE: Failure - Invalid Vehicle ID
            if (vehicle == null)
            {
                return (new DriverLoginResponse
                {
                    Success = false,
                    Message = $"Vehicle {request.VehicleId} not found",
                    Code = AuthResultCode.InvalidVehicle
                }, audit);
            }

            // CASE: Failure - Account lockout
            if (user.IsLockedOut == true && user.LockoutEnd > DateTime.UtcNow)
            {
                double remaining = Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalSeconds);
                return (new DriverLoginResponse
                {
                    Success = false,
                    Message = $"Account is locked. Try again in {remaining} second(s)",
                    Code = AuthResultCode.AccountLockout
                }, audit);
            }

            // CASE: Failure - Incorrect PIN
            if (!PasswordHandler.VerifyPassword(request.Pin, user.PasswordHash))
            {
                IncrementFailedAttempts(user, false);
                return (new DriverLoginResponse
                {
                    Success = false,
                    Message = "Incorrect PIN",
                    Code = AuthResultCode.InvalidPassword
                }, audit);
            }

            // Shift logic

            // CASE: Success
            ResetLockout(user);
            user.LastLogin = DateTime.UtcNow;
            audit.IsSuccess = true;

            return (new DriverLoginResponse
            {
                Success = true,
                Message = "Login successful",
                Token = GenerateJwtToken(user),
                DriverName = user.FullName ?? string.Empty,
                AssignedRouteId = ,
                ShiftId = ,
                Code = AuthResultCode.Success
            }, audit);
        }

        public override async Task<PasswordChangeResponse> PasswordChange(PasswordChangeRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var response = await ProcessPasswordChange(request, context);

                if (response.Success)                
                {   
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                else await transaction.RollbackAsync();

                return response;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error in AuthService during PasswordChange");

                return new PasswordChangeResponse
                {
                    Success = false,
                    Message = "An error occurred during password change. Please try again later.",
                    Code = AuthResultCode.Unknown
                };
            }
        }

        private async Task<PasswordChangeResponse> ProcessPasswordChange(
            PasswordChangeRequest request, ServerCallContext context)
        {
            // CASE: Failure - Invalid User ID format
            if (!int.TryParse(request.UserId, out int userId))
            {
                return new PasswordChangeResponse
                {
                    Success = false,
                    Message = "Invalid driver ID format",
                    Code = AuthResultCode.InvalidLoginFormat
                };
            }

            Shared.Models.User? user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);

            bool isStaff = user?.Role != null && !user.Role.RoleName.Equals("Driver", StringComparison.OrdinalIgnoreCase);
            int minPasswordLength = isStaff ? AuthDefaults.Staff.MinPasswordLength : AuthDefaults.Terminal.MinPasswordLength;
            int recentPasswordHistoryCount = isStaff ? AuthDefaults.Staff.RecentPasswordHistoryCount : AuthDefaults.Terminal.RecentPasswordHistoryCount;

            // CASE: Failure - Invalid User ID
            if (user == null)
            {
                return new PasswordChangeResponse
                {
                    Success = false,
                    Message = "An error occurred during password change. Please try again later.",
                    Code = AuthResultCode.Unknown
                };
            }

            // CASE: Failure - Account lockout
            if (user.IsLockedOut == true && user.LockoutEnd > DateTime.UtcNow)
            {
                double remaining = Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalSeconds);
                return new PasswordChangeResponse
                {
                    Success = false,
                    Message = $"Account is locked. Try again in {remaining} second(s)",
                    Code = AuthResultCode.AccountLockout
                };
            }

            // CASE: Failure - Incorrect new password format
            if (request.NewPassword.Length < minPasswordLength)
            {
                return new PasswordChangeResponse
                {
                    Success = false,
                    Message = $"New password must be at least {minPasswordLength} characters long",
                    Code = AuthResultCode.InvalidPasswordFormat
                };
            }

            // CASE: Failure - Incorrect current password
            if (!PasswordHandler.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            {
                IncrementFailedAttempts(user, isStaff);
                return new PasswordChangeResponse
                {
                    Success = false,
                    Message = "Incorrect current password",
                    Code = AuthResultCode.InvalidPassword
                };
            }

            string newHashedPassword = PasswordHandler.GetHashedPassword(request.NewPassword);

            // CASE: Failure - Password reuse
            if (IsPasswordReused(user, newHashedPassword, await GetRecentPasswordHistory(user.Id, isStaff)))
            {
                return new PasswordChangeResponse
                {
                    Success = false,
                    Message = $"New password must be different from the last {recentPasswordHistoryCount} passwords",
                    Code = AuthResultCode.InvalidPasswordHistory
                };
            }

            // CASE: Success
            _context.PasswordHistories.Add(new Shared.Models.PasswordHistory
            {
                UserId = user.Id,
                PasswordHash = user.PasswordHash,
                CreatedAt = DateTime.UtcNow
            });

            user.PasswordHash = newHashedPassword;
            ResetLockout(user);
            
            return new PasswordChangeResponse
            {
                Success = true,
                Message = "Password change successful",
                Code = AuthResultCode.Success
            };
        }              
    }
}
