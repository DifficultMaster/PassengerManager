using Microsoft.Extensions.Configuration;

namespace PassengerManager.Server.Services.Static
{
    public static class AppDefaults
    {
        public static class Sync
        {
            public static int StaticIntervalHours { get; private set; } = 24;

            public static int VehicleIntervalSeconds { get; private set; } = 5;

            public static int TripIntervalSeconds { get; private set; } = 15;

            internal static void Configure(IConfiguration configuration)
            {
                IConfigurationSection section = configuration.GetSection("AppDefaults:Sync");

                StaticIntervalHours = section.GetValue<int?>(nameof(StaticIntervalHours)) ?? StaticIntervalHours;
                VehicleIntervalSeconds = section.GetValue<int?>(nameof(VehicleIntervalSeconds)) ?? VehicleIntervalSeconds;
                TripIntervalSeconds = section.GetValue<int?>(nameof(TripIntervalSeconds)) ?? TripIntervalSeconds;
            }
        }

        public static void Configure(IConfiguration configuration)
        {
            Sync.Configure(configuration);
        }
    }
}
