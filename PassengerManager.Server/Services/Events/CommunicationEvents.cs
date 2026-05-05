using MassTransit;

namespace PassengerManager.Server.Services.Events
{
    public static class CommunicationEvents
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

        public sealed record CallInitiated(
            string CallId,
            int CallerUserId,
            string? CallerVehicleId,
            string? CallerRole,
            string? TargetDispatcherId,
            string CallType,
            string AgencyId,
            DateTime InitiatedAtUtc,
            string? FailureReason = null);

        public sealed record CallAssigned(
            string CallId,
            string AssignedDispatcherId,
            string? VehicleId,
            string AgencyId,
            DateTime AssignedAtUtc);

        public sealed record CallEnded(
            string CallId,
            string AgencyId,
            DateTime EndedAtUtc);
    }
}
