using System;
using System.Collections.Generic;
using System.Text;

namespace PassengerManager.Client.Core.DTOs
{
    public record GeoLocation(
        double Latitude,
        double Longitude,
        double Speed,
        double Bearing,
        double Odometer);
}
