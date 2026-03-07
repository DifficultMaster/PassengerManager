using System;
using System.Collections.Generic;
using System.Text;

namespace PassengerManager.Shared.DTOs
{
    public class TripUpdateDto
    {
        public string TripId { get; set; } = string.Empty;
        public string? VehicleId { get; set; }
        public int? DelaySeconds { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
