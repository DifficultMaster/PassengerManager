using CsvHelper.Configuration;
using PassengerManager.Server.Services.Maps.Converters;
using PassengerManager.Server.Services.Static;
using static PassengerManager.Server.Services.Static.MapConstraints;

namespace PassengerManager.Server.Services.Maps
{
    public sealed class StopMap : ClassMap<Shared.Models.Stop>
    {
        public StopMap()
        {
            Map(m => m.StopId).Name("stop_id")
                .TypeConverter(new MaxLengthConverter(MapConstraints.MaxKeyLength, truncate: true));

            Map(m => m.Name).Name("stop_name");

            Map(m => m.Latitude).Name("stop_lat");

            Map(m => m.Longitude).Name("stop_lon");

            Map(m => m.WheelchairBoarding).Name("wheelchair_boarding").Optional();

            Map(m => m.LocationType).Name("location_type").Optional();

            Map(m => m.PlatformCode).Name("platform_code").Optional();

            Map(m => m.Code).Name("stop_code")
                .TypeConverter(new MaxLengthConverter(MapConstraints.Stop.MaxCodeLength, truncate: true));
        }
    }
}
