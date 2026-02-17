using Microsoft.Extensions.Configuration;

namespace PassengerManager.Server.Services.Static
{
    public static class MapConstraints
    {
        public static int MaxKeyLength { get; private set; } = 128;

        public static class Agency
        {
            public static int MaxTimezoneLength { get; private set; } = 50;

            public static int MaxPhoneLength { get; private set; } = 50;

            public static int MaxLangLength { get; private set; } = 10;


            internal static void Configure(IConfiguration configuration)
            {
                IConfigurationSection section = configuration.GetSection("MapConstraints:Agency");

                MaxTimezoneLength = section.GetValue<int?>(nameof(MaxTimezoneLength)) ?? MaxTimezoneLength;
                MaxPhoneLength = section.GetValue<int?>(nameof(MaxPhoneLength)) ?? MaxPhoneLength;
                MaxLangLength = section.GetValue<int?>(nameof(MaxLangLength)) ?? MaxLangLength;
            }
        }

        public static class Route
        {
            public static int MaxColorLength { get; private set; } = 10;

            public static int MaxShortNameLength { get; private set; } = 50;

            internal static void Configure(IConfiguration configuration)
            {
                IConfigurationSection section = configuration.GetSection("MapConstraints:Route");

                MaxColorLength = section.GetValue<int?>(nameof(MaxColorLength)) ?? MaxColorLength;
                MaxShortNameLength = section.GetValue<int?>(nameof(MaxShortNameLength)) ?? MaxShortNameLength;
            }
        }

        public static class ShapePoint
        {
            internal static void Configure(IConfiguration configuration)
            {
                _ = configuration.GetSection("MapConstraints:ShapePoint");
            }
        }

        public static class Stop
        {
            public static int MaxCodeLength { get; private set; } = 50;

            internal static void Configure(IConfiguration configuration)
            {
                IConfigurationSection section = configuration.GetSection("MapConstraints:Stop");

                MaxCodeLength = section.GetValue<int?>(nameof(MaxCodeLength)) ?? MaxCodeLength;
            }
        }

        public static class Trip
        {
            internal static void Configure(IConfiguration configuration)
            {
                _ = configuration.GetSection("MapConstraints:Trip");
            }
        }

        public static void Configure(IConfiguration configuration)
        {
            IConfigurationSection section = configuration.GetSection("MapConstraints");
            MaxKeyLength = section.GetValue<int?>(nameof(MaxKeyLength)) ?? MaxKeyLength;

            Agency.Configure(configuration);
            Route.Configure(configuration);
            ShapePoint.Configure(configuration);
            Stop.Configure(configuration);
            Trip.Configure(configuration);
        }
    }     
}
