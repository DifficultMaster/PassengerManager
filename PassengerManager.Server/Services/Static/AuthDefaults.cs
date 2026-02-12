namespace PassengerManager.Server.Services.Static
{
    public abstract class AuthDefaults // use abstractiveness
    {
        public static class Staff
        {
            public const int MinPasswordLength = 8;
            public const int MaxFailedAttempts = 3;
            public const int LockoutDurationSeconds = 300;
            public const int RecentPasswordHistoryCount = 5;
        }

        public static class Terminal
        {
            public const int MinPasswordLength = 8;
            public const int MaxFailedAttempts = 1;
            public const int LockoutDurationSeconds = 30;
            public const int RecentPasswordHistoryCount = 5;
        }
    }
}
