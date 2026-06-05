using PassengerManager.Client.Core.DTOs;
using PassengerManager.Client.Core.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace PassengerManager.Client.Driver.Services.Location
{
    public class MockLocationProvider : ILocationProvider
    {
        private double _latitude = 47.9105;
        private double _longitude = 33.3918;
        private double _bearing = 45.0;
        private double _speed = 40.0;
        private double _odometer = 15000.0;
        private DateTime _lastUpdate = DateTime.UtcNow;

        public Task<GeoLocation> GetCurrentLocationAsync()
        {
            // Calculate drift based on time passed
            double secondsPassed = (DateTime.UtcNow - _lastUpdate).TotalSeconds;
            if (secondsPassed > 0)
            {
                double step = 0.00001 * secondsPassed;
                _latitude += step;
                _longitude += step;
                _bearing = (_bearing + 1) % 360;
                _odometer += (0.01 * secondsPassed);
                _lastUpdate = DateTime.UtcNow;
            }

            return Task.FromResult(new GeoLocation(_latitude, _longitude, _speed, _bearing, _odometer));
        }
    }
}
