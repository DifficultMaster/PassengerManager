namespace PassengerManager.Server.Services.Events
{
    public static class AuthEvents
    {
        public sealed record LoginAttempted(
            string Channel,
            string Login,
            bool Success,
            string Code,
            DateTime OccurredAtUtc,
            int? UserId = null,
            string? Role = null,
            string? VehicleId = null,
            long? ShiftId = null,
            string? FailureReason = null);

        public sealed record PasswordChanged(
            int ActorUserId,
            bool Success,
            string Code,
            DateTime OccurredAtUtc,
            string? FailureReason = null);

        public sealed record PasswordReset(
            int ActorUserId,
            int TargetUserId,
            bool Success,
            string Code,
            DateTime OccurredAtUtc,
            string? FailureReason = null);
    }
}
