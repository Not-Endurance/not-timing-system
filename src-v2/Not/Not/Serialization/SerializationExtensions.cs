using JsonNet.PrivatePropertySetterResolver;
using Newtonsoft.Json;

namespace Not.Serialization;

public static class SerializationExtensions
{
    private static readonly JsonSerializerSettings _settings = new()
    {
        ContractResolver = new PrivatePropertySetterResolver(),
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
        PreserveReferencesHandling = PreserveReferencesHandling.Objects,
        Formatting = Formatting.Indented,
    };

    public static string ToJson(this object obj)
    {
        return JsonConvert.SerializeObject(obj, _settings);
    }

    public static T FromJson<T>(this string json)
        where T : class
    {
        var result = JsonConvert.DeserializeObject<T>(json);
        if (result == default)
        {
            throw new Exception($"Cannot serialize '{json}' to type of '{typeof(T)}'");
        }
        return result;
    }
}
