using CsvHelper.Configuration;
using PassengerManager.Server.Services.Maps.Converters;
using PassengerManager.Server.Services.Static;
using static PassengerManager.Server.Services.Static.MapConstraints;

namespace PassengerManager.Server.Services.Maps
{
    public sealed class TripMap : ClassMap<Shared.Models.Trip>
    {
        public TripMap()
        {
            Map(m => m.TripId).Name("trip_id")
                .TypeConverter(new MaxLengthConverter(MapConstraints.MaxKeyLength, truncate: true));

            Map(m => m.RouteId).Name("route_id")
                .TypeConverter(new MaxLengthConverter(MapConstraints.MaxKeyLength, truncate: true));

            Map(m => m.ServiceId).Name("service_id")
                .TypeConverter(new MaxLengthConverter(MapConstraints.MaxKeyLength, truncate: true));

            Map(m => m.Headsign).Name("trip_headsign").Optional();

            Map(m => m.DirectionId).Name("direction_id").Optional();

            Map(m => m.ShapeId).Name("shape_id")
                .TypeConverter(new MaxLengthConverter(MapConstraints.MaxKeyLength, truncate: false)).Optional();
        }
    }
}
