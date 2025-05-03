using Newtonsoft.Json;

namespace Not.Serialization.JSON.Converters;

public class DateTimeOffsetUtcSerializer : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset ReadJson(
        JsonReader reader,
        Type objectType,
        DateTimeOffset existingValue,
        bool hasExistingValue,
        JsonSerializer serializer
    )
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return default;
        }
        var dateString = reader.Value?.ToString();
        if (reader.Value == null || string.IsNullOrWhiteSpace(dateString))
        {
            return default;
        }
        if (reader.TokenType == JsonToken.Date)
        {
            var dateTime = (DateTime)reader.Value;
            return new DateTimeOffset(dateTime.ToUniversalTime(), TimeSpan.Zero);
        }

        if (DateTimeOffset.TryParse(dateString, out var dateTimeOffset))
        {
            return dateTimeOffset.ToUniversalTime();
        }

        throw new JsonSerializationException($"Invalid DateTimeOffset value: {dateString}");
    }

    public override void WriteJson(JsonWriter writer, DateTimeOffset value, JsonSerializer serializer)
    {
        // Always write UTC time
        writer.WriteValue(value.UtcDateTime);
    }
}
