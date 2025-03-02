using JsonNet.PrivatePropertySetterResolver;
using Newtonsoft.Json;

namespace Not.Serialization;

public static class SerializationExtensions

{
    public static readonly JsonSerializerSettings SETTINGS = new()
    {
        ContractResolver = new PrivatePropertySetterResolver(),
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
        Formatting = Newtonsoft.Json.Formatting.Indented,
        TypeNameHandling = TypeNameHandling.Auto,
        ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
    };

    public static void AddConverter<T>(T converter)
        where T : JsonConverterBase
    {
        _converters.Add(converter);
    }


    /// <summary>
    /// Serializes the object by keeping single instance of uniqute (AggregateRoot.Id) object and replaces usages with #domainRef
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static string ToConvertedJson(this object obj)
    {
        // TODO probably separate in two serializers;
        AddConverters();
        lock (_lock)
        {
            var result = JsonConvert.SerializeObject(obj, SETTINGS);
            ResetConverters();
            RemoveConverters();
            return result;
        }
    }

    /// <summary>
    /// Deserializes the object and preserves instance equality (AggregateRoot.Id) by #domainRef with it's actual previously serialized instance
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="json"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static T FromConvertedJson<T>(this string json)
        where T : class
    {
        AddConverters();
        lock (_lock) // TODO: investigate serialization locking as a whole (reference serializer locks as well)
        {
            var result = JsonConvert.DeserializeObject<T>(json, SETTINGS);
            if (result == default)
            {
                throw new Exception($"Cannot serialize '{json}' to type of '{typeof(T)}'");
            }
            ResetConverters();
            RemoveConverters();
            return result;
        }
    }

    public static string ToJson(this object obj)
    {
        lock (_lock)
        {
            var result = JsonConvert.SerializeObject(obj, SETTINGS);
            return result;
        }
    }

    public static T FromJson<T>(this string json)
        where T : class
    {
        lock (_lock) // TODO: investigate serialization locking as a whole (reference serializer locks as well)
        {
            var result = JsonConvert.DeserializeObject<T>(json, SETTINGS);
            if (result == default)
            {
                throw new Exception($"Cannot serialize '{json}' to type of '{typeof(T)}'");
            }
            return result;
        }
    }

    static object _lock = new();
    static List<JsonConverterBase> _converters = [];

    static void ResetConverters()
    {
        foreach (var converter in _converters)
        {
            converter.Reset();
        }
    }

    static void AddConverters()
    {
        foreach (var converter in _converters)
        {
            SETTINGS.Converters.Add(converter);
        }
    }

    static void RemoveConverters()
    {
        foreach (var converter in _converters)
        {
            SETTINGS.Converters.Remove(converter);
        }
    }
}

public abstract class JsonConverterBase : JsonConverter
{
    public abstract void Reset();
}
