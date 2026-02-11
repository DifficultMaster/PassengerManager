using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace PassengerManager.Server.Services.Maps.Converters
{
    public sealed class PhoneConverter : DefaultTypeConverter
    {
        private readonly int _maxLength;
        private readonly bool _truncate;

        public PhoneConverter(int maxLength, bool truncate = false)
        {
            _maxLength = maxLength;
            _truncate = truncate;
        }

        public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            string value = text.Trim();
            if (value.Length > _maxLength)
                if (_truncate)
                {
                    value.Replace("+", "").Replace("-", "").Replace(" ", "").Replace("_", "");
                    if (value.Length > _maxLength) return null;
                }
                else return null;

            return value;
        }
    }
}
