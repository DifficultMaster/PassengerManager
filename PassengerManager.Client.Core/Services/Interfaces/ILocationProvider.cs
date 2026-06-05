using System;
using System.Collections.Generic;
using System.Text;
using PassengerManager.Client.Core.DTOs;

namespace PassengerManager.Client.Core.Services.Interfaces
{
    public interface ILocationProvider
    {
        Task<GeoLocation> GetCurrentLocationAsync();
    }
}
