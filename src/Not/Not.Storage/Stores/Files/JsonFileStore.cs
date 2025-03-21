using Newtonsoft.Json;
using Not.Filesystem;
using Not.Serialization.JSON;

namespace Not.Storage.Stores.Files;

public static class JsonFileStore
{
    internal static readonly JsonSerializerSettings SETTINGS = new NJsonSettings();
    static readonly object LOCK = new();
    static readonly List<JsonConverterBase> CONVERTERS = [];

    public static void AddConverter<TConverter>(TConverter converter)
        where TConverter : JsonConverterBase
    {
        SETTINGS.Converters.Add(converter);
        CONVERTERS.Add(converter);
    }

    public static string ToJson(object obj)
    {
        lock (LOCK)
        {
            var result = JsonConvert.SerializeObject(obj, SETTINGS);
            ResetConverters();
            return result;
        }
    }

    public static T FromJson<T>(string json)
        where T : class, new()
    {
        lock (LOCK) // TODO: investigate serialization locking as a whole (reference serializer locks as well)
        {
            var result = JsonConvert.DeserializeObject<T>(json, SETTINGS);
            if (result == default)
            {
                throw new Exception($"Cannot serialize '{json}' to type of '{typeof(T)}'");
            }
            ResetConverters();
            return result;
        }
    }

    static void ResetConverters()
    {
        foreach (var converter in CONVERTERS)
        {
            converter.Reset();
        }
    }
}

public abstract class JsonFileStore<T>
    where T : class, new()
{
    readonly string _path;

    protected JsonFileStore(string path)
    {
        _path = path;
    }

    protected void Serialize(T value)
    {
        var contents = JsonFileStore.ToJson(value);
        FileHelper.Write(contents, _path);
    }

    protected async Task SerializeAsync(T value)
    {
        var contents = JsonFileStore.ToJson(value);
        await FileHelper.WriteAsync(contents, _path);
    }

    protected async Task<T> DeserializeAsync()
    {
        var contents = await FileHelper.SafeReadStringAsync(_path);
        return contents == null ? new() : JsonFileStore.FromJson<T>(contents);
    }
}
