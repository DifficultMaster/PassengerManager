using CsvHelper.Configuration;
using PassengerManager.Server.Services.Maps.Converters;
using PassengerManager.Server.Services.Static;
using static PassengerManager.Server.Services.Static.MapConstraints;

namespace PassengerManager.Server.Services.Maps
{
    public sealed class RouteMap : ClassMap<Shared.Models.Route>
    {       
        public RouteMap()
        {
            Map(m => m.RouteId).Name("route_id")
                .TypeConverter(new MaxLengthConverter(MapConstraints.MaxKeyLength, truncate: true));

            Map(m => m.AgencyId).Name("agency_id")
                .TypeConverter(new MaxLengthConverter(MapConstraints.MaxKeyLength, truncate: true));

            Map(m => m.ShortName).Convert(args =>
            {
                string? shortName = args.Row.GetField("route_short_name");
                if (!string.IsNullOrWhiteSpace(shortName))
                {
                    return shortName;
                }

                string? longName = args.Row.GetField("route_long_name");
                if (string.IsNullOrWhiteSpace(longName))
                {
                    return "Депо";
                }

                return longName.Length < MapConstraints.Route.MaxShortNameLength ? longName : longName.Substring(0, MapConstraints.Route.MaxShortNameLength);
            });

            Map(m => m.LongName).Name("route_long_name").Optional();

            Map(m => m.Description).Name("route_desc").Optional();

            Map(m => m.Type).Name("route_type");

            Map(m => m.Url).Name("route_url").Optional();

            Map(m => m.SortOrder).Name("route_sort_order").Optional();

            Map(m => m.Color).Name("route_color")
                .TypeConverter(new MaxLengthConverter(MapConstraints.Route.MaxColorLength, truncate: false)).Optional();

            Map(m => m.TextColor).Name("route_text_color")
                .TypeConverter(new MaxLengthConverter(MapConstraints.Route.MaxColorLength, truncate: false)).Optional();
        }
    }
}
