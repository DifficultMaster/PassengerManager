using System;
using System.Collections.Generic;
using System.Text;

namespace PassengerManager.Shared.DTOs
{
    public class VehiclePositionDto
    {
        public string VehicleId { get; set; } = string.Empty;
        public string? LicensePlate { get; set; }
        public string? RouteId { get; set; }
        public string? TripId { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public float? Bearing { get; set; }
        public double? Speed { get; set; }
        public double? Odometer { get; set; }

        public int? CurrentStatus { get; set; }
        public string? StopId { get; set; }
        public int? CurrentStopSequence { get; set; }
        public int? CongestionLevel { get; set; }
        public int? OccupancyStatus { get; set; }

        public DateTime Timestamp { get; set; }
    }
}
