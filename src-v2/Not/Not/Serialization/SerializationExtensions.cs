using JsonNet.PrivatePropertySetterResolver;
using Newtonsoft.Json;

namespace Not.Serialization;

public static class SerializationExtensions
{

    private static List<JsonConverterBase> _converters = [];

    private static readonly JsonSerializerSettings _settings = new()
    {
        ContractResolver = new PrivatePropertySetterResolver(),
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
        Formatting = Formatting.Indented,
        TypeNameHandling = TypeNameHandling.All,
        ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
    };

    public static void AddConverter<T>(T converter)
        where T : JsonConverterBase
    {
        _converters.Add(converter);
        _settings.Converters.Add(converter);
    }

    public static string ToJson(this object obj)
    {
        var result = JsonConvert.SerializeObject(obj, _settings);
        ResetConverters();
        return result;
    }

    public static T FromJson<T>(this string json)
        where T : class
    {
        var result = JsonConvert.DeserializeObject<T>(json, _settings);
        if (result == default)
        {
            throw new Exception($"Cannot serialize '{json}' to type of '{typeof(T)}'");
        }
        ResetConverters();
        return result;
    }

    static void ResetConverters()
    {
        foreach (var converter in _converters)
        {
            converter.Reset();
        }
    }
}

public abstract class JsonConverterBase : JsonConverter
{
    public abstract void Reset(); 
}
