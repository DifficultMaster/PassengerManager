using CsvHelper.Configuration;
using PassengerManager.Server.Services.Maps.Converters;
using PassengerManager.Server.Services.Static;
using static PassengerManager.Server.Services.Static.MapConstraints;

namespace PassengerManager.Server.Services.Maps
{
    public sealed class AgencyMap : ClassMap<Shared.Models.Agency>
    {
        public AgencyMap()
        {
            Map(m => m.AgencyId).Name("agency_id")
                .TypeConverter(new MaxLengthConverter(MapConstraints.MaxKeyLength, truncate: true));

            Map(m => m.Name).Name("agency_name");

            Map(m => m.Url).Name("agency_url");

            Map(m => m.Timezone).Name("agency_timezone")
                .TypeConverter(new MaxLengthConverter(MapConstraints.Agency.MaxTimezoneLength, truncate: true));

            Map(m => m.Lang).Name("agency_lang")
                .TypeConverter(new MaxLengthConverter(MapConstraints.Agency.MaxLangLength, truncate: true)).Optional();

            Map(m => m.Phone).Name("agency_phone")
                .TypeConverter(new PhoneConverter(MapConstraints.Agency.MaxPhoneLength, truncate: true)).Optional();
        }
    }
}
