using MassTransit;

namespace PassengerManager.Server.Services.Events
{
    public static class DriverOpsEvents
    {
        public sealed record ShiftEnded(
            int UserId,
            bool Success,
            string Code,
            DateTime EndDate,            
            string? Role = null,
            string? VehicleId = null,
            string? AgencyId = null,
            long? ShiftId = null);

        public sealed record IncidentReported(
            int UserId,
            bool Success,
            DateTime OccurredAtUtc,
            string IncidentType,
            long? ShiftId = null,
            string? VehicleId = null,
            string? AgencyId = null,
            string? RouteId = null,
            string? AlertId = null,
            int? GtfsCause = null,
            int? GtfsEffect = null,
            string? FailureReason = null);
    }
}
