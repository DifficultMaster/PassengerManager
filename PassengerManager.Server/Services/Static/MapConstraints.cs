namespace PassengerManager.Server.Services.Static
{
    public abstract class MapConstraints
    {
        public const int MaxKeyLength = 128;

        public static class Agency
        {
            public const int MaxTimezoneLength = 50;
            public const int MaxPhoneLength = 50;
            public const int MaxLangLength = 10;
        }

        public static class Route
        {
            public const int MaxColorLength = 10;
            public const int MaxShortNameLength = 50;
        }

        public static class ShapePoint
        {
        }

        public static class Stop
        {
            public const int MaxCodeLength = 50;
        }

        public static class Trip
        {
        }
    }     
}
