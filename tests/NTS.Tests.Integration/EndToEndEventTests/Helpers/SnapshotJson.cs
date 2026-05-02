using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using Not.Serialization.JSON;

namespace NTS.Tests.Integration.EndToEndEventTests.Helpers;

internal static class SnapshotJson
{
    //TODO: extract automatically or provide by consumers
    static readonly string[] DATE_PROPERTIES =
    [
        "Start",
        "StartDay",
        "EndDay",
        "StartTimeOverride",
    ];
    static readonly string[] TIME_PROPERTIES =
    [
        "StartTime",
        "ArriveTime",
        "PresentTime",
        "RepresentTime",
        "RequiredInspectionTime",
        "OutTime",
        "LastArriveTime",
    ];

    public static JToken NormalizeMongoDocument(JToken token)
    {
        if (token is JArray array)
        {
            return new JArray(array.Select(NormalizeMongoDocument));
        }

        if (token is not JObject source)
        {
            return token.DeepClone();
        }

        if (source.Count == 1 && source.Property("$date")?.Value is JValue dateValue)
        {
            return new JValue(dateValue.Value?.ToString());
        }

        var result = new JObject();
        foreach (var property in source.Properties())
        {
            var name = property.Name == "_id" ? "Id" : property.Name;
            result[name] = NormalizeMongoDocument(property.Value);
        }

        return result;
    }

    public static JToken Canonicalize(object value)
    {
        return Canonicalize(JToken.FromObject(value, Serializer));
    }

    public static JToken Canonicalize(JToken token)
    {
        return token switch
        {
            JObject obj => CanonicalizeObject(obj),
            JArray array => new JArray(array.Select(Canonicalize)),
            JValue value => CanonicalizeValue(value),
            _ => token.DeepClone(),
        };
    }

    public static void ReplaceIds(JToken token, IReadOnlyDictionary<int, int> idMap)
    {
        if (token is JArray array)
        {
            foreach (var item in array)
            {
                ReplaceIds(item, idMap);
            }

            return;
        }

        if (token is not JObject obj)
        {
            return;
        }

        foreach (var property in obj.Properties().ToArray())
        {
            if (
                (property.Name == "Id" || property.Name == "EventId")
                && TryReadInteger(property.Value, out var id)
                && idMap.TryGetValue(id, out var replacement)
            )
            {
                property.Value = replacement;
                continue;
            }

            ReplaceIds(property.Value, idMap);
        }
    }

    public static JToken Parse(string json)
    {
        using var stringReader = new StringReader(json);
        using var jsonReader = new JsonTextReader(stringReader) { DateParseHandling = DateParseHandling.None };
        return JToken.Load(jsonReader);
    }

    public static JsonSerializer Serializer { get; } = JsonSerializer.Create(new NJsonSettings());

    static JObject CanonicalizeObject(JObject source)
    {
        var result = new JObject();
        foreach (var property in source.Properties().OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            var name = property.Name == "_id" ? "Id" : property.Name;
            var value = Canonicalize(property.Value);
            if (DATE_PROPERTIES.Contains(name) && value.Type == JTokenType.String)
            {
                value = CanonicalizeDate(value.Value<string>());
            }
            else if (TIME_PROPERTIES.Contains(name) && value.Type == JTokenType.String)
            {
                value = CanonicalizeTime(value.Value<string>());
            }
            result[name] = value;
        }

        return result;
    }

    static JValue CanonicalizeValue(JValue value)
    {
        if (value.Type is JTokenType.Integer or JTokenType.Float)
        {
            return new JValue(Convert.ToDecimal(value.Value, CultureInfo.InvariantCulture));
        }
        if (value.Type == JTokenType.Date)
        {
            return CanonicalizeDateValue(value.Value);
        }

        return new JValue(value.Value);
    }

    static JValue CanonicalizeDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new JValue(value);
        }

        var date = DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return new JValue(date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }

    static JValue CanonicalizeTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new JValue(value);
        }

        if (TimeSpan.TryParseExact(value, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var time))
        {
            return new JValue(time.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture));
        }

        var date = DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return new JValue(date.ToLocalTime().TimeOfDay.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture));
    }

    static JValue CanonicalizeDateValue(object? value)
    {
        var date =
            value switch
            {
                DateTimeOffset offset => offset,
                DateTime dateTime => new DateTimeOffset(dateTime),
                _ => DateTimeOffset.Parse(
                    value?.ToString() ?? "",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind
                ),
            };
        return new JValue(date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }

    static bool TryReadInteger(JToken token, out int value)
    {
        if (token.Type == JTokenType.Integer)
        {
            value = token.Value<int>();
            return true;
        }

        value = 0;
        return false;
    }
}
