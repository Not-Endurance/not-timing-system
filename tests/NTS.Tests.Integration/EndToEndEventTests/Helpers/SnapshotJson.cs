using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Not.Serialization.JSON;

namespace NTS.Tests.Integration.EndToEndEventTests.Helpers;

internal static class SnapshotJson
{
    //TODO: extract automatically or provide by consumers
    static readonly string[] DATE_PROPERTIES = ["Start", "StartDay", "EndDay", "StartTimeOverride"];
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

    public static JToken NormalizeNames(JToken token)
    {
        var normalized = token.DeepClone();
        NormalizeNamesCore(normalized, context: null);
        return normalized;
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
            if (value.Type == JTokenType.Null)
            {
                continue;
            }
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
        var date = value switch
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

    static void NormalizeNamesCore(JToken token, string? context)
    {
        if (token is JArray array)
        {
            foreach (var item in array)
            {
                NormalizeNamesCore(item, context);
            }

            return;
        }

        if (token is not JObject obj)
        {
            return;
        }

        if (TryReadJoinedNames(obj, out var joinedName))
        {
            if (string.IsNullOrWhiteSpace(obj.Value<string>("Name")))
            {
                obj["Name"] = joinedName;
            }

            obj.Remove("Names");
        }

        if (
            IsActorContext(context)
            && string.IsNullOrWhiteSpace(obj.Value<string>("Name"))
            && !string.IsNullOrWhiteSpace(obj.Value<string>("NameEnglish"))
        )
        {
            obj["Name"] = obj.Value<string>("NameEnglish");
        }

        foreach (var property in obj.Properties().ToArray())
        {
            NormalizeNamesCore(property.Value, ResolveContext(property.Name, context));
        }
    }

    static bool TryReadJoinedNames(JObject obj, out string joinedName)
    {
        joinedName = string.Empty;
        if (obj["Names"] is JArray names)
        {
            joinedName = string.Join(
                ' ',
                names.Select(x => x.Value<string>()).Where(x => !string.IsNullOrWhiteSpace(x))
            );
            return !string.IsNullOrWhiteSpace(joinedName);
        }

        if (
            obj["Names"] is JValue { Type: JTokenType.String } value
            && !string.IsNullOrWhiteSpace(value.Value<string>())
        )
        {
            joinedName = value.Value<string>()!;
            return true;
        }

        return false;
    }

    static string? ResolveContext(string propertyName, string? context)
    {
        return (propertyName, context) switch
        {
            ("Athlete", _) => "athlete",
            ("Horse", _) => "horse",
            ("Officials", _) => "official",
            ("Operators", _) => "operator",
            ("SnapshotHistory", _) => "snapshotGroup",
            ("SnapshotGroups", _) => "snapshotGroup",
            ("Entries", "snapshotGroup") => "snapshot",
            _ => null,
        };
    }

    static bool IsActorContext(string? context)
    {
        return context is "athlete" or "horse" or "official" or "operator" or "snapshot";
    }
}
