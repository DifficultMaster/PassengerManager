using CsvHelper.Configuration;

namespace PassengerManager.Server.Services.Maps
{
    public sealed class AgencyMap : ClassMap<Shared.Models.Agency>
    {
        public AgencyMap()
        {
            Map(m => m.AgencyId).Name("agency_id");

            Map(m => m.Name).Name("agency_name");

            Map(m => m.Url).Name("agency_url");

            Map(m => m.Timezone).Name("agency_timezone");

            Map(m => m.Lang).Name("agency_lang").Optional();

            Map(m => m.Phone).Name("agency_phone").Optional();
        }
    }
}
