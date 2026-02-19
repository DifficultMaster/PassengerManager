using PassengerManager.Shared.Models;
using System;
using System.Collections.Generic;
using System.Text;
using PassengerManager.Shared.Protos;

namespace PassengerManager.Client.Core.Services.Interfaces
{
    public interface IAuthService
    {
        Task<DriverLoginResponse> AuthenticateDriverAsync(DriverLoginRequest request);
    }
}
