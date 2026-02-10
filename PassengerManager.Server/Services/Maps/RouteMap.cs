using CsvHelper.Configuration;

namespace PassengerManager.Server.Services.Maps
{
    public sealed class RouteMap : ClassMap<Shared.Models.Route>
    {
        public RouteMap()
        {
            Map(m => m.RouteId).Name("route_id");

            Map(m => m.AgencyId).Name("agency_id");

            Map(m => m.ShortName).Convert(args =>
            {
                string? shortName = args.Row.GetField("route_short_name");
                if (!string.IsNullOrWhiteSpace(shortName))
                {
                    return shortName;
                }

                string longName = args.Row.GetField("route_long_name");
                if (string.IsNullOrWhiteSpace(longName))
                {
                    return "Депо";
                }

                return longName.Length < 50 ? longName : longName.Substring(0, 50);
            });

            Map(m => m.LongName).Name("route_long_name").Optional();

            Map(m => m.Description).Name("route_desc").Optional();

            Map(m => m.Type).Name("route_type");

            Map(m => m.Url).Name("route_url").Optional();

            Map(m => m.SortOrder).Name("route_sort_order").Optional();

            Map(m => m.Color).Name("route_color").Optional(); // how to ensure these are not added if they are over set in db char limit

            Map(m => m.TextColor).Name("route_text_color").Optional();
        }
    }
}
