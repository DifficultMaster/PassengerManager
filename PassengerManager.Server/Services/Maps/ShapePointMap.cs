using CsvHelper.Configuration;
using PassengerManager.Server.Services.Maps.Converters;
using PassengerManager.Server.Services.Static;
using static PassengerManager.Server.Services.Static.MapConstraints;

namespace PassengerManager.Server.Services.Maps
{
    public sealed class ShapePointMap : ClassMap<Shared.Models.ShapePoint>
    {
        public ShapePointMap()
        {
            Map(m => m.ShapeId).Name("shape_id")
                .TypeConverter(new MaxLengthConverter(MapConstraints.MaxKeyLength, truncate: true));

            Map(m => m.Latitude).Name("shape_pt_lat");

            Map(m => m.Longitude).Name("shape_pt_lon");

            Map(m => m.Sequence).Name("shape_pt_sequence");

            Map(m => m.DistTraveled).Name("shape_dist_traveled").Optional();
        }
    }
}
