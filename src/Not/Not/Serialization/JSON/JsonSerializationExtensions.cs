using Newtonsoft.Json;

namespace Not.Serialization.JSON;

public static class JsonSerializationExtensions
{
    static readonly JsonSerializerSettings SETTINGS = new NJsonSettings();

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

}

public abstract class JsonConverterBase : JsonConverter
{
    public abstract void Reset();
}
