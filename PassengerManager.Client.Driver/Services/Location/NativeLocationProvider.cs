using System;
using System.Collections.Generic;
using System.Text;
using Windows.Devices.Geolocation;
using PassengerManager.Client.Core.DTOs;
using PassengerManager.Client.Core.Services.Interfaces;

namespace PassengerManager.Client.Driver.Services.Location
{
    public class NativeLocationProvider : ILocationProvider
    {
        private Geolocator? _geolocator;
        private double _mockOdometer = 0;

        public async Task<GeoLocation> GetCurrentLocationAsync()
        {
            if (_geolocator == null)
            {
                var accessStatus = await Geolocator.RequestAccessAsync();
                if (accessStatus == GeolocationAccessStatus.Allowed)
                {
                    _geolocator = new Geolocator()
                    {
                        DesiredAccuracyInMeters = 5
                    };
                }
                else
                {
                    throw new UnauthorizedAccessException("Windows Location Services are disabled or denied");
                }
            }

            Geoposition position = await _geolocator.GetGeopositionAsync();
            var coords = position.Coordinate;

            double speed = coords.Speed ?? 0;
            double bearing = coords.Heading ?? 0;

            _mockOdometer += speed;

            return new GeoLocation(
                coords.Point.Position.Latitude,
                coords.Point.Position.Longitude,
                speed * 3.6,
                bearing,
                _mockOdometer
            );
        }
    }
}
