using Newtonsoft.Json;

namespace Not.Serialization.JSON;

public static class JsonSerializationExtensions
{
    static readonly JsonSerializerSettings SETTINGS = new NJsonSettings();
    static readonly object LOCK = new();

    public static string ToJson(this object obj)
    {
        lock (LOCK)
        {
            var result = JsonConvert.SerializeObject(obj, SETTINGS);
            return result;
        }
    }

    public static T FromJson<T>(this string json)
        where T : class
    {
        lock (LOCK)
        {
            var result = JsonConvert.DeserializeObject<T>(json, SETTINGS);
            if (result == default)
            {
                throw new Exception($"Cannot serialize '{json}' to type of '{typeof(T)}'");
            }
            return result;
        }
    }

    public static T? TryFromJson<T>(this string json)
        where T : class
    {
        lock (LOCK)
        {
            return JsonConvert.DeserializeObject<T>(json, SETTINGS);
        }
    }
}

public abstract class JsonConverterBase : JsonConverter
{
    public abstract void Reset();
}
