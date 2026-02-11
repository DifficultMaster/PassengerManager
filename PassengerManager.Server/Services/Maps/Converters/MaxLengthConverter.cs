using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace PassengerManager.Server.Services.Maps.Converters
{
    public sealed class MaxLengthConverter : DefaultTypeConverter
    {
        private readonly int _maxLength;
        private readonly bool _truncate;

        public MaxLengthConverter(int maxLength, bool truncate = false)
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
                return _truncate ? value.Substring(0, _maxLength) : null;

            return value;
        }
    }
}
