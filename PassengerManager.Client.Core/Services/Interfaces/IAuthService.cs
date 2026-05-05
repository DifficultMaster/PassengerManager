using PassengerManager.Shared.Models;
using System;
using System.Collections.Generic;
using System.Text;
using PassengerManager.Shared.Protos;

namespace PassengerManager.Client.Core.Services.Interfaces
{
    public interface IAuthService
    {
        /// <summary>
        /// Authenticates the hardware device using its vehicle ID and hardware hash.
        /// This provides a long-lived hardware token for continuous telemetry.
        /// </summary>
        Task<HardwareLoginResponse> AuthenticateHardwareAsync(HardwareLoginRequest request);

        Task<DriverLoginResponse> AuthenticateDriverAsync(DriverLoginRequest request);

        Task<PasswordChangeResponse> ChangeDriverPasswordAsync(PasswordChangeRequest request, string tempToken);
    }
}
