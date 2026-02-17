using Microsoft.Extensions.Configuration;

namespace PassengerManager.Server.Services.Static
{
    public static class TimeoutDefaults
    {
        public static class StaticData
        {
            public static int MaxRetries { get; private set; } = 5;
            public static int RetryTimeoutSeconds { get; private set; } = 10;
            public static int DownloadTimeoutSeconds { get; private set; } = 300;

            internal static void Configure(IConfiguration configuration)
            {
                IConfigurationSection section = configuration.GetSection("TimeoutDefaults:StaticData");

                MaxRetries = section.GetValue<int?>(nameof(MaxRetries)) ?? MaxRetries;
                RetryTimeoutSeconds = section.GetValue<int?>(nameof(RetryTimeoutSeconds)) ?? RetryTimeoutSeconds;
                DownloadTimeoutSeconds = section.GetValue<int?>(nameof(DownloadTimeoutSeconds)) ?? DownloadTimeoutSeconds;
            }
        }

        public static class VehicleData
        {
            public static int DownloadTimeoutSeconds { get; private set; } = 5;

            internal static void Configure(IConfiguration configuration)
            {
                IConfigurationSection section = configuration.GetSection("TimeoutDefaults:VehicleData");

                DownloadTimeoutSeconds = section.GetValue<int?>(nameof(DownloadTimeoutSeconds)) ?? DownloadTimeoutSeconds;
            }
        }

        public static class TripData
        {
            public static int DownloadTimeoutSeconds { get; private set; } = 15;

            internal static void Configure(IConfiguration configuration)
            {
                IConfigurationSection section = configuration.GetSection("TimeoutDefaults:TripData");

                DownloadTimeoutSeconds = section.GetValue<int?>(nameof(DownloadTimeoutSeconds)) ?? DownloadTimeoutSeconds;
            }
        }

        public static void Configure(IConfiguration configuration)
        {
            StaticData.Configure(configuration);
            VehicleData.Configure(configuration);
            TripData.Configure(configuration);
        }
    }
}
