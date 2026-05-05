using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PassengerManager.Server.Extensions;
using PassengerManager.Server.Models;
using PassengerManager.Server.Services.Events;
using PassengerManager.Server.Services.Interfaces;
using PassengerManager.Server.Services.Security;
using PassengerManager.Server.Services.Static;
using PassengerManager.Shared.Models;
using PassengerManager.Shared.Protos;
using PasswordGenerator;
using System;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using static PassengerManager.Server.Services.Static.AuthDefaults;

namespace PassengerManager.Server.Services
{
    public class AuthService : PassengerManager.Shared.Protos.AuthService.AuthServiceBase
    {
        private readonly ILogger<AuthService> _logger;
        private readonly Models.PassengerManagerContext _context;
        private readonly ITokenService _tokenService;
        private readonly IMessageService _messageService;

        public AuthService(ILogger<AuthService> logger, Models.PassengerManagerContext context, ITokenService tokenService, IMessageService messageService)
        {
            _logger = logger;
            _context = context;
            _tokenService = tokenService;
            _messageService = messageService;
        }

        private string GeneratePassword(User targetUser)
        {
            if (targetUser.Role != null && targetUser.Role.RoleName.Equals("Driver", StringComparison.OrdinalIgnoreCase))
            {
               return new Password(
                        includeLowercase: false,
                        includeUppercase: false,
                        includeNumeric: true,
                        includeSpecial: false,
                        passwordLength: AuthDefaults.Terminal.DefaultPasswordLength
                    ).Next();
            }
            else
            {
                return new Password(
                        includeLowercase: true,
                        includeUppercase: true,
                        includeNumeric: true,
                        includeSpecial: true,
                        passwordLength: AuthDefaults.Staff.DefaultPasswordLength
                    ).Next();
            }
        }

        [AllowAnonymous]
        public override async Task<StaffLoginResponse> StaffLogin(StaffLoginRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();

            Shared.Models.User? user = null;
            StaffLoginResponse response = new StaffLoginResponse
            {
                Success = false,
                Message = "An error occurred during login. Please try again later",
                Code = AuthResultCode.Unknown
            };

            try
            {
                Shared.Models.LoginAudit audit = new Shared.Models.LoginAudit
                {
                    UsernameAttempted = request.Username,
                    AttemptTime = DateTime.UtcNow,
                    IpAddress = context.Peer,
                    UserAgent = "Desktop Client",
                    IsSuccess = false,
                };

                user = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Username == request.Username);

                // CASE: Failure - Invalid User ID
                if (user == null)
                {
                    response.Success = false;
                    response.Message = "Incorrect username";
                    response.Code = AuthResultCode.InvalidLogin;
                }

                // CASE: Failure - Wrong UI
                else if (user.Role == null || user.Role.RoleName.Equals("Driver", StringComparison.OrdinalIgnoreCase))
                {
                    audit.UserId = user.Id;
                    response.Success = false;
                    response.Message = user.Role == null
                        ? "User has no assigned role"
                        : "Drivers must use terminal mode to log in";
                    response.Code = AuthResultCode.InvalidMode;
                }

                // CASE: Failure - Account lockout
                else if (user.IsLockedOut == true && user.LockoutEnd > DateTime.UtcNow)
                {
                    audit.UserId = user.Id;
                    response.Success = false;
                    response.Message = "Account is locked";
                    response.Code = AuthResultCode.AccountLockout;
                }

                // CASE: Failure - Incorrect password
                else if (!PasswordHandler.VerifyPassword(request.Password, user.PasswordHash))
                {
                    DateTime cutoff = DateTime.UtcNow.AddSeconds(-AuthDefaults.Staff.LockoutDurationSeconds);
                    DateTime? lastAttemptTime = await _context.LoginAudits
                        .AsNoTracking()
                        .Where(a => a.UsernameAttempted == request.Username)
                        .OrderByDescending(a => a.AttemptTime)
                        .Select(a => (DateTime?)a.AttemptTime)
                        .FirstOrDefaultAsync();

                    if (lastAttemptTime.HasValue && lastAttemptTime.Value < cutoff)
                    {
                        user.FailedLoginAttempts = 0;
                    }

                    audit.UserId = user.Id;
                    user.FailedLoginAttempts = (user.FailedLoginAttempts ?? 0) + 1;
                    if (user.FailedLoginAttempts >= AuthDefaults.Staff.MaxFailedAttempts)
                    {
                        user.IsLockedOut = true;
                        user.LockoutEnd = DateTime.UtcNow.AddSeconds(AuthDefaults.Staff.LockoutDurationSeconds);

                        response.Success = false;
                        response.Message = "Account is locked";
                        response.Code = AuthResultCode.AccountLockout;
                    }

                    response.Success = false;
                    response.Message = "Incorrect password";
                    response.Code = AuthResultCode.InvalidPassword;
                }                      
                
                else
                {
                    DateTime? lastChangeDate = await _context.PasswordHistories
                    .AsNoTracking()
                    .Where(h => h.UserId == user.Id)
                    .OrderByDescending(h => h.CreatedAt)
                    .Select(h => (DateTime?)h.CreatedAt)
                    .FirstOrDefaultAsync();

                    DateTime effectiveDate = lastChangeDate ?? DateTime.MinValue;

                    // CASE: Failure - Password change overdue
                    if (effectiveDate.AddDays(AuthDefaults.Staff.MaxPasswordAgeDays) < DateTime.UtcNow)
                    {
                        audit.UserId = user.Id;
                        response.Success = false;
                        response.Message = "Password expired";
                        response.Code = AuthResultCode.CredentialOverdue;
                        response.Token = _tokenService.GenerateIdToken(user);
                    }

                    // CASE: Success
                    else
                    {
                        audit.UserId = user.Id;
                        audit.IsSuccess = true;
                        user.FailedLoginAttempts = 0;
                        user.IsLockedOut = false;
                        user.LockoutEnd = null;
                        user.LastLogin = DateTime.UtcNow;

                        response.Success = true;
                        response.Message = "Login successful";
                        response.Token = _tokenService.GenerateIdToken(user);
                        response.FullName = user.FullName ?? string.Empty;
                        response.RoleName = user.Role.RoleName;
                        response.AccessLevel = user.Role.AccessLevel;
                        response.DefaultWindow = user.Role.DefaultWindow ?? string.Empty;
                        response.Code = AuthResultCode.Success;
                    }                    
                }

                _context.LoginAudits.Add(audit);

                using var transaction = await _context.Database.BeginTransactionAsync();
                {
                    try
                    {
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AuthService during StaffLogin");

                response.Success = false;
                response.Message = "An error occurred during login. Please try again later";
                response.Code = AuthResultCode.Unknown;
                return response;
            }
            finally
            {
                await _messageService.PublishSafeAsync(
                    new AuthEvents.LoginAttempted(
                        Channel: "staff",
                        Login: request.Username,
                        Success: response.Success,
                        Code: response.Code.ToString(),
                        OccurredAtUtc: DateTime.UtcNow,
                        UserId: user?.Id,
                        Role: user?.Role?.RoleName,
                        FailureReason: response.Success ? null : response.Message),
                    "Auth.LoginAttempted",
                    context.CancellationToken);
            }
        }

        [AllowAnonymous]
        public override async Task<HardwareLoginResponse> HardwareLogin(HardwareLoginRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();

            HardwareLoginResponse response = new HardwareLoginResponse
            {
                Success = false,
                Message = "An error occurred during login. Please try again later",
                Code = AuthResultCode.Unknown
            };

            try
            {
                Shared.Models.Vehicle? vehicle = await _context.Vehicles.FindAsync(request.VehicleId);

                // CASE: Failure - Vehicle does not exist or is marked as inactive
                if (vehicle == null || (vehicle.IsActive != null && vehicle.IsActive == false))
                {
                    response.Success = false;
                    response.Message = "Device unauthorized or suspended";
                    response.Code = AuthResultCode.InvalidVehicle;                   
                }

                // CASE: Failure - Incorrect hash password
                else if (!PasswordHandler.VerifyPassword(request.HardwareHash, vehicle.HardwareHash))
                {
                    response.Success = false;
                    response.Message = "Invalid credentials";
                    response.Code = AuthResultCode.InvalidPassword;
                }

                // CASE: Success
                else
                {
                    response.Success = true;
                    response.Message = "Login successful";
                    response.Code = AuthResultCode.Success;
                    response.Token = _tokenService.GenerateHardwareToken(vehicle.VehicleId, vehicle.AgencyId);
                }                

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AuthService during HardwareLogin");

                response.Success = false;
                response.Message = "An error occurred during login. Please try again later";
                response.Code = AuthResultCode.Unknown;

                return response;
            }
            finally
            {
                await _messageService.PublishSafeAsync(
                   new AuthEvents.LoginAttempted(
                       Channel: "hardware",
                       Login: request.VehicleId,
                       Success: response.Success,
                       Code: response.Code.ToString(),
                       OccurredAtUtc: DateTime.UtcNow,
                       UserId: null,
                       Role: null,
                       VehicleId: request.VehicleId,
                       ShiftId: null,
                       FailureReason: response.Success ? null : response.Message),
                   "Auth.LoginAttempted",
                   context.CancellationToken);
            }            
        }

        [AllowAnonymous]
        public override async Task<DriverLoginResponse> DriverLogin(DriverLoginRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();

            DriverLoginResponse response = new DriverLoginResponse
            {
                Success = false,
                Message = "An error occurred during login. Please try again later",
                Code = AuthResultCode.Unknown
            };
            Shared.Models.User? driverUser = null;
            Shared.Models.Shift? newShift = null;

            try
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
                    response.Success = false;
                    response.Message = "Invalid driver ID format";
                    response.Code = AuthResultCode.InvalidLoginFormat;
                }
                else
                {
                    Shared.Models.User? user = await _context.Users
                        .Include(u => u.Role)
                        .FirstOrDefaultAsync(u => u.Id == driverId);

                    // CASE: Failure - Invalid User ID
                    if (user == null)
                    {
                        response.Success = false;
                        response.Message = $"Driver {request.UserId} not found";
                        response.Code = AuthResultCode.InvalidLogin;
                    }
                    // CASE: Failure - Wrong UI
                    else if (user.Role == null || !user.Role.RoleName.Equals("Driver", StringComparison.OrdinalIgnoreCase))
                    {
                        response.Success = false;
                        response.Message = user.Role == null
                            ? "User has no assigned role"
                            : "Staff must use desktop mode to log in";
                        response.Code = AuthResultCode.InvalidMode;
                    }
                    else
                    {
                        Shared.Models.Vehicle? vehicle = await _context.Vehicles
                            .FindAsync(request.VehicleId);

                        // CASE: Failure - Invalid Vehicle ID
                        if (vehicle == null)
                        {
                            response.Success = false;
                            response.Message = $"Vehicle {request.VehicleId} not found";
                            response.Code = AuthResultCode.InvalidVehicle;
                        }
                        // CASE: Failure - Vehicle not provisioned for hardware fingerprint
                        else if (string.IsNullOrWhiteSpace(vehicle.HardwareHash)
                            || vehicle.HardwareHash.Equals("UNSET", StringComparison.OrdinalIgnoreCase))
                        {
                            response.Success = false;
                            response.Message = $"Vehicle {request.VehicleId} is not provisioned";
                            response.Code = AuthResultCode.InvalidVehicle;
                        }
                        // CASE: Failure - Account lockout
                        else if (user.IsLockedOut == true && user.LockoutEnd > DateTime.UtcNow)
                        {
                            response.Success = false;
                            response.Message = "Account is locked";
                            response.Code = AuthResultCode.AccountLockout;
                        }
                        // CASE: Failure - Incorrect PIN
                        else if (!PasswordHandler.VerifyPassword(request.Pin, user.PasswordHash))
                        {
                            DateTime cutoff = DateTime.UtcNow.AddSeconds(-AuthDefaults.Staff.LockoutDurationSeconds);
                            DateTime? lastAttemptTime = await _context.LoginAudits
                                .AsNoTracking()
                                .Where(a => a.UsernameAttempted == request.UserId)
                                .OrderByDescending(a => a.AttemptTime)
                                .Select(a => (DateTime?)a.AttemptTime)
                                .FirstOrDefaultAsync();

                            if (lastAttemptTime.HasValue && lastAttemptTime.Value < cutoff)
                            {
                                user.FailedLoginAttempts = 0;
                            }

                            user.FailedLoginAttempts = (user.FailedLoginAttempts ?? 0) + 1;
                            if (user.FailedLoginAttempts >= AuthDefaults.Terminal.MaxFailedAttempts)
                            {
                                user.IsLockedOut = true;
                                user.LockoutEnd = DateTime.UtcNow.AddSeconds(AuthDefaults.Staff.LockoutDurationSeconds);

                                response.Success = false;
                                response.Message = "Account is locked";
                                response.Code = AuthResultCode.AccountLockout;
                            }

                            response.Success = false;
                            response.Message = "Incorrect PIN";
                            response.Code = AuthResultCode.InvalidPassword;
                        }                        
                        else
                        {
                            DateTime? lastChangeDate = await _context.PasswordHistories
                                .AsNoTracking()
                                .Where(h => h.UserId == user.Id)
                                .OrderByDescending(h => h.CreatedAt)
                                .Select(h => (DateTime?)h.CreatedAt)
                                .FirstOrDefaultAsync();

                            DateTime effectiveDate = lastChangeDate ?? DateTime.MinValue;

                            // CASE: Failure - Password change overdue
                            if (effectiveDate.AddDays(AuthDefaults.Terminal.MaxPasswordAgeDays) < DateTime.UtcNow)
                            {
                                audit.UserId = user.Id;
                                response.Success = false;
                                response.Message = "Password expired";
                                response.Code = AuthResultCode.CredentialOverdue;
                                response.Token = _tokenService.GenerateIdToken(user);
                            }

                            // CASE: Success
                            else
                            {
                                audit.UserId = user.Id;
                                audit.IsSuccess = true;
                                user.FailedLoginAttempts = 0;
                                user.IsLockedOut = false;
                                user.LockoutEnd = null;
                                user.LastLogin = DateTime.UtcNow;
                                driverUser = user;

                                List<Shared.Models.Shift> openShifts = await _context.Shifts
                                    .Where(s => s.VehicleId == request.VehicleId && s.EndTime == null)
                                    .ToListAsync();

                                foreach (Shared.Models.Shift shift in openShifts)
                                {
                                    shift.EndTime = DateTime.UtcNow;
                                }

                                newShift = new Shared.Models.Shift
                                {
                                    UserId = user.Id,
                                    VehicleId = request.VehicleId,
                                    StartTime = DateTime.UtcNow,
                                    IsApproved = true,
                                };

                                response.Success = true;
                                response.Message = "Login successful";
                                response.DriverName = user.FullName ?? string.Empty;
                                response.Code = AuthResultCode.Success;
                            }                            
                        }
                    }
                }

                _context.LoginAudits.Add(audit);

                if (newShift != null)
                {
                    _context.Shifts.Add(newShift);
                }

                using var transaction = await _context.Database.BeginTransactionAsync();
                {
                    try
                    {
                        await _context.SaveChangesAsync();

                        if (response.Success && newShift != null && driverUser != null)
                        {
                            response.ShiftId = newShift.Id;
                            response.Token = _tokenService.GenerateDriverToken(driverUser, newShift.Id, newShift.VehicleId, driverUser.AgencyId);
                        }

                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AuthService during DriverLogin");

                response.Success = false;
                response.Message = "An error occurred during login. Please try again later";
                response.Code = AuthResultCode.Unknown;
                return response;
            }
            finally
            {
                await _messageService.PublishSafeAsync(
                    new AuthEvents.LoginAttempted(
                        Channel: "driver",
                        Login: request.UserId,
                        Success: response.Success,
                        Code: response.Code.ToString(),
                        OccurredAtUtc: DateTime.UtcNow,
                        UserId: driverUser?.Id,
                        Role: driverUser?.Role?.RoleName,
                        VehicleId: request.VehicleId,
                        ShiftId: response.Success ? response.ShiftId : null,
                        FailureReason: response.Success ? null : response.Message),
                    "Auth.LoginAttempted",
                    context.CancellationToken);
            }
        }

        [Authorize]
        public override async Task<PasswordChangeResponse> PasswordChange(PasswordChangeRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();

            ClaimsPrincipal? userPrincipal = context.GetHttpContext().User;
            int actorUserId = userPrincipal?.GetUserId() ?? -1;
            PasswordChangeResponse response = new PasswordChangeResponse
            {
                Success = false,
                Message = "An error occurred during password change. Please try again later.",
                Code = AuthResultCode.Unknown
            };

            try
            {
                int userId = userPrincipal.GetUserId();

                Shared.Models.User? user = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                // CASE: Failure - Invalid User ID
                if (user == null)
                {
                    response.Success = false;
                    response.Message = "An error occurred during password change. Please try again later.";
                    response.Code = AuthResultCode.Unknown;
                }
                else
                {
                    bool isStaff = user.Role != null && !user.Role.RoleName.Equals("Driver", StringComparison.OrdinalIgnoreCase);
                    int minPasswordLength = isStaff ? AuthDefaults.Staff.MinPasswordLength : AuthDefaults.Terminal.MinPasswordLength;
                    int recentPasswordHistoryCount = isStaff ? AuthDefaults.Staff.RecentPasswordHistoryCount : AuthDefaults.Terminal.RecentPasswordHistoryCount;

                    // CASE: Failure - Account lockout
                    if (user.IsLockedOut == true && user.LockoutEnd > DateTime.UtcNow)
                    {
                        double remaining = Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalSeconds);
                        response.Success = false;
                        response.Message = $"Account is locked. Try again in {remaining} second(s)";
                        response.Code = AuthResultCode.AccountLockout;
                    }

                    // CASE: Failure - Incorrect new password format
                    else if (request.NewPassword.Length < minPasswordLength)
                    {
                        response.Success = false;
                        response.Message = $"New password must be at least {minPasswordLength} characters long";
                        response.Code = AuthResultCode.InvalidPasswordFormat;
                    }

                    // CASE: Failure - Incorrect new password format
                    else if (!isStaff && request.NewPassword.Any(c => !char.IsDigit(c)))
                    {
                        response.Success = false;
                        response.Message = $"Driver passwords can only be numeric";
                        response.Code = AuthResultCode.InvalidPasswordFormat;
                    }

                    // CASE: Failure - Incorrect current password
                    else if (!PasswordHandler.VerifyPassword(request.CurrentPassword, user.PasswordHash))
                    {
                        user.FailedLoginAttempts = (user.FailedLoginAttempts ?? 0) + 1;
                        int maxFailedAttempts = isStaff ? AuthDefaults.Staff.MaxFailedAttempts : AuthDefaults.Terminal.MaxFailedAttempts;
                        if (user.FailedLoginAttempts >= maxFailedAttempts)
                        {
                            user.IsLockedOut = true;
                            user.LockoutEnd = DateTime.UtcNow.AddSeconds(AuthDefaults.Staff.LockoutDurationSeconds);
                        }

                        response.Success = false;
                        response.Message = "Incorrect current password";
                        response.Code = AuthResultCode.InvalidPassword;
                    }
                    else
                    {
                        // CASE: Failure - Password reuse
                        string newHashedPassword = PasswordHandler.GetHashedPassword(request.NewPassword);

                        List<Shared.Models.PasswordHistory> recentHistory = await _context.PasswordHistories
                            .Where(h => h.UserId == user.Id)
                            .OrderByDescending(h => h.CreatedAt)
                            .Take(recentPasswordHistoryCount)
                            .ToListAsync();

                        if (user.PasswordHash == newHashedPassword || recentHistory.Any(h => h.PasswordHash == newHashedPassword))
                        {
                            response.Success = false;
                            response.Message = $"New password must be different from the last {recentPasswordHistoryCount} passwords";
                            response.Code = AuthResultCode.InvalidPasswordHistory;
                        }
                        else
                        {
                            // CASE: Success
                            user.PasswordHash = newHashedPassword;
                            user.FailedLoginAttempts = 0;
                            user.IsLockedOut = false;
                            user.LockoutEnd = null;

                            _context.PasswordHistories.Add(new Shared.Models.PasswordHistory
                            {
                                UserId = user.Id,
                                PasswordHash = user.PasswordHash,
                                CreatedAt = DateTime.UtcNow
                            });

                            using var transaction = await _context.Database.BeginTransactionAsync();
                            {
                                try
                                {
                                    await _context.SaveChangesAsync();
                                    await transaction.CommitAsync();
                                }
                                catch
                                {
                                    await transaction.RollbackAsync();
                                    throw;
                                }
                            }

                            response.Success = true;
                            response.Message = "Password change successful";
                            response.Code = AuthResultCode.Success;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AuthService during PasswordChange");

                response.Success = false;
                response.Message = "An error occurred during password change. Please try again later.";
                response.Code = AuthResultCode.Unknown;
            }
            finally
            {
                await _messageService.PublishSafeAsync(
                    new AuthEvents.PasswordChanged(
                        ActorUserId: actorUserId,
                        Success: response.Success,
                        Code: response.Code.ToString(),
                        OccurredAtUtc: DateTime.UtcNow,
                        FailureReason: response.Success ? null : response.Message),
                    "Auth.PasswordChanged",
                    context.CancellationToken);
            }

            return response;
        }

        [Authorize]
        public override async Task<PasswordResetResponse> PasswordReset(PasswordResetRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();

            ClaimsPrincipal? user = context.GetHttpContext().User;
            int actorUserId = user?.GetUserId() ?? -1;
            PasswordResetResponse response = new PasswordResetResponse
            {
                Success = false,
                Message = "Internal server error",
                Code = AuthResultCode.Unknown
            };

            try
            {
                // CASE: Failure - Invalid User ID
                if (user == null || !int.TryParse(user.FindFirst("AccessLevel")?.Value, out int userLevel))
                {
                    response.Success = false;
                    response.Message = "An error occurred during password reset. Please try again later.";
                    response.Code = AuthResultCode.Unknown;
                }

                // CASE: Failure - Account prohibited
                else if (!user.IsInRole("Admin") && !user.IsInRole("SuperAdmin"))
                {
                    _logger.LogCritical($"Unauthorized admin usage detected: IP:'{context.Peer}' on PasswordReset at AuthService");

                    response.Success = false;
                    response.Message = "Access denied";
                    response.Code = AuthResultCode.Unauthorized;
                }
                else
                {
                    Shared.Models.User? targetUser = await _context.Users
                        .Include(u => u.Role)
                        .FirstOrDefaultAsync(u => u.Id == request.TargetUserId);

                    // CASE: Failure - Invalid target user id
                    if (targetUser == null)
                    {
                        response.Success = false;
                        response.Message = "Target user is invalid";
                        response.Code = AuthResultCode.InvalidTarget;
                    }
                    else
                    {
                        int targetLevel = targetUser.Role?.AccessLevel ?? 0;

                        // CASE: Failure - Invalid target user role
                        if (targetLevel >= userLevel)
                        {
                            response.Success = false;
                            response.Message = "Only users with lower access level may change the target's password";
                            response.Code = AuthResultCode.InvalidRole;
                        }
                        else
                        {
                            // CASE: Success
                            targetUser.PasswordHash = PasswordHandler.GetHashedPassword(GeneratePassword(targetUser));
                            targetUser.FailedLoginAttempts = 0;
                            targetUser.IsLockedOut = false;
                            targetUser.LockoutEnd = null;

                            _context.PasswordHistories.Add(new Shared.Models.PasswordHistory
                            {
                                UserId = targetUser.Id,
                                PasswordHash = targetUser.PasswordHash,
                                CreatedAt = DateTime.UtcNow
                            });

                            using var transaction = await _context.Database.BeginTransactionAsync();
                            {
                                try
                                {
                                    await _context.SaveChangesAsync();
                                    await transaction.CommitAsync();
                                }
                                catch
                                {
                                    await transaction.RollbackAsync();
                                    throw;
                                }
                            }

                            response.Success = true;
                            response.Message = "Password reset successful";
                            response.Code = AuthResultCode.Success;
                            response.Generated = targetUser.PasswordHash;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AuthService during PasswordReset");

                response.Success = false;
                response.Message = "Internal server error";
                response.Code = AuthResultCode.Unknown;
            }
            finally
            {
                await _messageService.PublishSafeAsync(
                    new AuthEvents.PasswordReset(
                        ActorUserId: actorUserId,
                        TargetUserId: request.TargetUserId,
                        Success: response.Success,
                        Code: response.Code.ToString(),
                        OccurredAtUtc: DateTime.UtcNow,
                        FailureReason: response.Success ? null : response.Message),
                    "Auth.PasswordReset",
                    context.CancellationToken);
            }

            return response;
        }
    }
}

// todo logging interceptor avoid sensitive data