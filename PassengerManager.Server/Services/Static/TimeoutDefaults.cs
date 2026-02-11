namespace PassengerManager.Server.Services.Static
{
    public abstract class TimeoutDefaults
    {
        public static class StaticData
        {
            public const int MaxRetries = 5;
            public const int RetryTimeoutSeconds = 10;
            public const int DownloadTimeoutSeconds = 300;
        }

        public static class VehicleData
        {
            public const int MaxRetries = 5;
            public const int RetryTimeoutSeconds = 10;
            public const int DownloadTimeoutSeconds = 300;
        }

        public static class TripData
        {
            public const int MaxRetries = 5;
            public const int RetryTimeoutSeconds = 10;
            public const int DownloadTimeoutSeconds = 300;
        }
    }
}