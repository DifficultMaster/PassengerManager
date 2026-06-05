using System;
using System.Collections.Generic;
using System.Text;
using PassengerManager.Client.Core.DTOs;
using PassengerManager.Client.Core.Services.Interfaces;

namespace PassengerManager.Client.Driver.Services.Location
{
    public class SmartDebugLocationProvider : ILocationProvider
    {
        private readonly NativeLocationProvider _nativeProvider = new();
        private readonly MockLocationProvider _mockProvider = new();
        private bool _useMockFallback = false;

        public async Task<GeoLocation> GetCurrentLocationAsync()
        {
            if (_useMockFallback)
            {
                return await _mockProvider.GetCurrentLocationAsync();
            }

            try
            {
                return await _nativeProvider.GetCurrentLocationAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Native GPS failed, falling back to mock: {ex.Message}");
                _useMockFallback = true;

                return await _mockProvider.GetCurrentLocationAsync();
            }
        }
    }
}
