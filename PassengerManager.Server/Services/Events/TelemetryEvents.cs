namespace PassengerManager.Server.Services.Events
{
    public static class TelemetryEvents
    {
        public sealed record HeartbeatReceived(
            string VehicleId,
            double Latitude,
            double Longitude,
            double? Bearing,
            double? Speed,
            double? Odometer,
            string? AgencyId,
            string? RouteId,
            string? TripId
            );

        public sealed record StatusReceived(
            string VehicleId,
            string? AgencyId,
            int? CurrentStatus = null,
            int? CurrentStopSequence = null,
            int? CongestionLevel = null,
            int? OccupancyStatus = null
            );
    }
}
