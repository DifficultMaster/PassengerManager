using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PassengerManager.Server.Extensions;
using PassengerManager.Server.Models;
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

        public AuthService(ILogger<AuthService> logger, Models.PassengerManagerContext context, ITokenService tokenService)
        {
            _logger = logger;
            _context = context;
            _tokenService = tokenService;
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

                Shared.Models.User? user = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Username == request.Username);

                StaffLoginResponse response;

                // CASE: Failure - Invalid User ID
                if (user == null)
                {
                    response = new StaffLoginResponse
                    {
                        Success = false,
                        Message = "Incorrect username",
                        Code = AuthResultCode.InvalidLogin
                    };
                }

                // CASE: Failure - Wrong UI
                else if (user.Role == null || user.Role.RoleName.Equals("Driver", StringComparison.OrdinalIgnoreCase))
                {
                    audit.UserId = user.Id;
                    response = new StaffLoginResponse
                    {
                        Success = false,
                        Message = user.Role == null
                            ? "User has no assigned role"
                            : "Drivers must use terminal mode to log in",
                        Code = AuthResultCode.InvalidMode
                    };
                }

                // CASE: Failure - Account lockout
                else if (user.IsLockedOut == true && user.LockoutEnd > DateTime.UtcNow)
                {
                    audit.UserId = user.Id;
                    double remaining = Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalSeconds);
                    response = new StaffLoginResponse
                    {
                        Success = false,
                        Message = $"Account is locked. Try again in {remaining} second(s)",
                        Code = AuthResultCode.AccountLockout
                    };
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
                    }

                    response = new StaffLoginResponse
                    {
                        Success = false,
                        Message = "Incorrect password",
                        Code = AuthResultCode.InvalidPassword
                    };
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
                        response = new StaffLoginResponse
                        {
                            Success = false,
                            Message = "Password expired",
                            Code = AuthResultCode.CredentialOverdue,
                            Token = _tokenService.GenerateIdToken(user)
                        };
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

                        response = new StaffLoginResponse
                        {
                            Success = true,
                            Message = "Login successful",
                            Token = _tokenService.GenerateIdToken(user),
                            FullName = user.FullName ?? string.Empty,
                            RoleName = user.Role.RoleName,
                            AccessLevel = user.Role.AccessLevel,
                            DefaultWindow = user.Role.DefaultWindow ?? string.Empty,
                            Code = AuthResultCode.Success
                        };
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

                return new StaffLoginResponse
                {
                    Success = false,
                    Message = "An error occurred during login. Please try again later",
                    Code = AuthResultCode.Unknown
                };
            }
        }

        [AllowAnonymous]
        public override async Task<DriverLoginResponse> DriverLogin(DriverLoginRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();

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

                DriverLoginResponse response;
                Shared.Models.Shift? newShift = null;
                Shared.Models.User? driverUser = null;

                // CASE: Failure - Invalid User ID format
                if (!int.TryParse(request.UserId, out int driverId))
                {
                    response = new DriverLoginResponse
                    {
                        Success = false,
                        Message = "Invalid driver ID format",
                        Code = AuthResultCode.InvalidLoginFormat
                    };
                }
                else
                {
                    Shared.Models.User? user = await _context.Users
                        .Include(u => u.Role)
                        .FirstOrDefaultAsync(u => u.Id == driverId);

                    // CASE: Failure - Invalid User ID
                    if (user == null)
                    {
                        response = new DriverLoginResponse
                        {
                            Success = false,
                            Message = $"Driver {request.UserId} not found",
                            Code = AuthResultCode.InvalidLogin
                        };
                    }
                    // CASE: Failure - Wrong UI
                    else if (user.Role == null || !user.Role.RoleName.Equals("Driver", StringComparison.OrdinalIgnoreCase))
                    {
                        response = new DriverLoginResponse
                        {
                            Success = false,
                            Message = user.Role == null
                                ? "User has no assigned role"
                                : "Staff must use desktop mode to log in",
                            Code = AuthResultCode.InvalidMode
                        };
                    }
                    else
                    {
                        Shared.Models.Vehicle? vehicle = await _context.Vehicles
                            .FindAsync(request.VehicleId);

                        // CASE: Failure - Invalid Vehicle ID
                        if (vehicle == null)
                        {
                            response = new DriverLoginResponse
                            {
                                Success = false,
                                Message = $"Vehicle {request.VehicleId} not found",
                                Code = AuthResultCode.InvalidVehicle
                            };
                        }
                        // CASE: Failure - Account lockout
                        else if (user.IsLockedOut == true && user.LockoutEnd > DateTime.UtcNow)
                        {
                            double remaining = Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalSeconds);
                            response = new DriverLoginResponse
                            {
                                Success = false,
                                Message = $"Account is locked. Try again in {remaining} second(s)",
                                Code = AuthResultCode.AccountLockout
                            };
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
                            }

                            response = new DriverLoginResponse
                            {
                                Success = false,
                                Message = "Incorrect PIN",
                                Code = AuthResultCode.InvalidPassword
                            };
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
                                response = new DriverLoginResponse
                                {
                                    Success = false,
                                    Message = "Password expired",
                                    Code = AuthResultCode.CredentialOverdue,
                                    Token = _tokenService.GenerateIdToken(user)
                                };
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

                                List<string> availableRoutes = await _context.Routes
                                    .Where(r => r.AgencyId == vehicle.AgencyId)
                                    .OrderBy(r => r.ShortName)
                                    .Select(r => r.ShortName)
                                    .ToListAsync();

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
                                    RouteId = null,
                                    CurrentTripId = null
                                };

                                response = new DriverLoginResponse
                                {
                                    Success = true,
                                    Message = "Login successful",
                                    DriverName = user.FullName ?? string.Empty,
                                    Code = AuthResultCode.Success
                                };

                                response.AvailableRoutes.AddRange(availableRoutes);
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
                            response.Token = _tokenService.GenerateDriverToken(driverUser, newShift.Id, newShift.VehicleId);
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

                return new DriverLoginResponse
                {
                    Success = false,
                    Message = "An error occurred during login. Please try again later",
                    Code = AuthResultCode.Unknown
                };
            }
        }

        [Authorize]
        public override async Task<PasswordChangeResponse> PasswordChange(PasswordChangeRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();

            try
            {
                ClaimsPrincipal? userPrincipal = context.GetHttpContext().User;
                int userId = userPrincipal.GetUserId();

                Shared.Models.User? user = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Id == userId);

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

                bool isStaff = user.Role != null && !user.Role.RoleName.Equals("Driver", StringComparison.OrdinalIgnoreCase);
                int minPasswordLength = isStaff ? AuthDefaults.Staff.MinPasswordLength : AuthDefaults.Terminal.MinPasswordLength;
                int recentPasswordHistoryCount = isStaff ? AuthDefaults.Staff.RecentPasswordHistoryCount : AuthDefaults.Terminal.RecentPasswordHistoryCount;

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

                // CASE: Failure - Incorrect new password format
                if (!isStaff && request.NewPassword.Any(c => !char.IsDigit(c)))
                {
                    return new PasswordChangeResponse
                    {
                        Success = false,
                        Message = $"Driver passwords can only be numeric",
                        Code = AuthResultCode.InvalidPasswordFormat
                    };
                }

                // CASE: Failure - Incorrect current password
                if (!PasswordHandler.VerifyPassword(request.CurrentPassword, user.PasswordHash))
                {
                    user.FailedLoginAttempts = (user.FailedLoginAttempts ?? 0) + 1;
                    int maxFailedAttempts = isStaff ? AuthDefaults.Staff.MaxFailedAttempts : AuthDefaults.Terminal.MaxFailedAttempts;
                    if (user.FailedLoginAttempts >= maxFailedAttempts)
                    {
                        user.IsLockedOut = true;
                        user.LockoutEnd = DateTime.UtcNow.AddSeconds(AuthDefaults.Staff.LockoutDurationSeconds);
                    }

                    return new PasswordChangeResponse
                    {
                        Success = false,
                        Message = "Incorrect current password",
                        Code = AuthResultCode.InvalidPassword
                    };
                }

                // CASE: Failure - Password reuse
                string newHashedPassword = PasswordHandler.GetHashedPassword(request.NewPassword);

                List<Shared.Models.PasswordHistory> recentHistory = await _context.PasswordHistories
                    .Where(h => h.UserId == user.Id)
                    .OrderByDescending(h => h.CreatedAt)
                    .Take(recentPasswordHistoryCount)
                    .ToListAsync();

                if (user.PasswordHash == newHashedPassword || recentHistory.Any(h => h.PasswordHash == newHashedPassword))
                {
                    return new PasswordChangeResponse
                    {
                        Success = false,
                        Message = $"New password must be different from the last {recentPasswordHistoryCount} passwords",
                        Code = AuthResultCode.InvalidPasswordHistory
                    };
                }

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

                return new PasswordChangeResponse
                {
                    Success = true,
                    Message = "Password change successful",
                    Code = AuthResultCode.Success
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AuthService during PasswordChange");

                return new PasswordChangeResponse
                {
                    Success = false,
                    Message = "An error occurred during password change. Please try again later.",
                    Code = AuthResultCode.Unknown
                };
            }
        }

        [Authorize]
        public override async Task<PasswordResetResponse> PasswordReset(PasswordResetRequest request, ServerCallContext context)
        {
            _context.ChangeTracker.Clear();

            try
            {
                ClaimsPrincipal? user = context.GetHttpContext().User;                

                // CASE: Failure - Invalid User ID
                if (user == null || !int.TryParse(user.FindFirst("AccessLevel")?.Value, out int userLevel))
                {
                    return new PasswordResetResponse
                    {
                        Success = false,
                        Message = "An error occurred during password reset. Please try again later.",
                        Code = AuthResultCode.Unknown
                    };
                }                             

                // CASE: Failure - Account prohibited
                if (!user.IsInRole("Admin") && !user.IsInRole("SuperAdmin"))
                {
                    _logger.LogCritical($"Unauthorized admin usage detected: IP:'{context.Peer}' on PasswordReset at AuthService");

                    return new PasswordResetResponse
                    {
                        Success = false,
                        Message = $"Access denied",
                        Code = AuthResultCode.Unauthorized
                    };
                }

                Shared.Models.User? targetUser = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Id == request.TargetUserId);   
                
                // CASE: Failure - Invalid target user id
                if (targetUser == null)
                {
                    return new PasswordResetResponse
                    {
                        Success = false,
                        Message = $"Target user is invalid",
                        Code = AuthResultCode.InvalidTarget
                    };
                }

                int targetLevel = targetUser.Role?.AccessLevel ?? 0;

                // CASE: Failure - Invalid target user role
                if (targetLevel >= userLevel)
                {
                    return new PasswordResetResponse
                    {
                        Success = false,
                        Message = $"Only users with lower access level may change the target's password",
                        Code = AuthResultCode.InvalidRole
                    };
                }                           

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

                return new PasswordResetResponse
                {
                    Success = true,
                    Message = "Password reset successful",
                    Code = AuthResultCode.Success,
                    Generated = targetUser.PasswordHash
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AuthService during PasswordReset");

                return new PasswordResetResponse
                {
                    Success = false,
                    Message = "Internal server error",
                    Code = AuthResultCode.Unknown
                };
            }
        }
    }
}

// todo logging interceptor avoid sensitive data