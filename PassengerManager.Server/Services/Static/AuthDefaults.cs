using Microsoft.Extensions.Configuration;

namespace PassengerManager.Server.Services.Static
{
    public static class AuthDefaults
    {
        public static class Staff
        {
            public static int MinPasswordLength { get; private set; } = 8;

            public static int DefaultPasswordLength { get; private set; } = 12;

            public static int MaxPasswordAgeDays { get; private set; } = 90;

            public static int MaxFailedAttempts { get; private set; } = 3;

            public static int LockoutDurationSeconds { get; private set; } = 300;

            public static int RecentPasswordHistoryCount { get; private set; } = 5;

            internal static void Configure(IConfiguration configuration)
            {
                IConfigurationSection section = configuration.GetSection("AuthDefaults:Staff");

                MinPasswordLength = section.GetValue<int?>(nameof(MinPasswordLength)) ?? MinPasswordLength;
                DefaultPasswordLength = section.GetValue<int?>(nameof(DefaultPasswordLength)) ?? DefaultPasswordLength;
                MaxPasswordAgeDays = section.GetValue<int?>(nameof(MaxPasswordAgeDays)) ?? MaxPasswordAgeDays;
                MaxFailedAttempts = section.GetValue<int?>(nameof(MaxFailedAttempts)) ?? MaxFailedAttempts;
                LockoutDurationSeconds = section.GetValue<int?>(nameof(LockoutDurationSeconds)) ?? LockoutDurationSeconds;
                RecentPasswordHistoryCount = section.GetValue<int?>(nameof(RecentPasswordHistoryCount)) ?? RecentPasswordHistoryCount;
            }
        }

        public static class Terminal
        {
            public static int MinPasswordLength { get; private set; } = 8;

            public static int DefaultPasswordLength { get; private set; } = 8;

            public static int MaxPasswordAgeDays { get; private set; } = 30;

            public static int MaxFailedAttempts { get; private set; } = 1;

            public static int LockoutDurationSeconds { get; private set; } = 30;

            public static int RecentPasswordHistoryCount { get; private set; } = 5;

            internal static void Configure(IConfiguration configuration)
            {
                IConfigurationSection section = configuration.GetSection("AuthDefaults:Terminal");

                MinPasswordLength = section.GetValue<int?>(nameof(MinPasswordLength)) ?? MinPasswordLength;
                DefaultPasswordLength = section.GetValue<int?>(nameof(DefaultPasswordLength)) ?? DefaultPasswordLength;
                MaxPasswordAgeDays = section.GetValue<int?>(nameof(MaxPasswordAgeDays)) ?? MaxPasswordAgeDays;
                MaxFailedAttempts = section.GetValue<int?>(nameof(MaxFailedAttempts)) ?? MaxFailedAttempts;
                LockoutDurationSeconds = section.GetValue<int?>(nameof(LockoutDurationSeconds)) ?? LockoutDurationSeconds;
                RecentPasswordHistoryCount = section.GetValue<int?>(nameof(RecentPasswordHistoryCount)) ?? RecentPasswordHistoryCount;
            }
        }

        public static void Configure(IConfiguration configuration)
        {
            Staff.Configure(configuration);
            Terminal.Configure(configuration);
        }
    }
}
