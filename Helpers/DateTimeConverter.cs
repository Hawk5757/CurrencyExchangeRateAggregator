using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace CurrencyExchangeRateAggregator.Helpers;

public class DateTimeConverter : JsonConverter<DateTime>
{
    private readonly string _dateTimeFormat;

    public DateTimeConverter(string dateTimeFormat)
    {
        _dateTimeFormat = dateTimeFormat;
    }

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateTime.ParseExact(reader.GetString(), _dateTimeFormat, CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(_dateTimeFormat, CultureInfo.InvariantCulture));
    }
}