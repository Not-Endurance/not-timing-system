using Newtonsoft.Json;
using Not.Filesystem;
using Not.Serialization.JSON;

namespace Not.Storage.Stores.Files;

public static class JsonFileStore
{
    internal static readonly JsonSerializerSettings SETTINGS = new NJsonSettings();
    static readonly SemaphoreSlim SEMAPHORE = new(1);
    static readonly List<JsonConverterBase> CONVERTERS = [];

    public static void AddConverter<TConverter>(TConverter converter)
        where TConverter : JsonConverterBase
    {
        SETTINGS.Converters.Add(converter);
        CONVERTERS.Add(converter);
    }

    public static async Task Write(string path, object obj)
    {
        await SEMAPHORE.WaitAsync();
        try
        {
            var result = JsonConvert.SerializeObject(obj, SETTINGS);
            ResetConverters();
            await FileHelper.WriteAsync(path, result);
        }
        finally
        {
            SEMAPHORE.Release();
        }
    }

    public static async Task<T?> Read<T>(string path)
        where T : class
    {
        await SEMAPHORE.WaitAsync();
        try // TODO: investigate serialization locking as a whole (reference serializer locks as well)
        {
            var json = await FileHelper.SafeReadStringAsync(path);
            if (json == null)
            {
                return null;
            }
            var result = JsonConvert.DeserializeObject<T>(json, SETTINGS);
            if (result == default)
            {
                throw new Exception($"Cannot serialize '{json}' to type of '{typeof(T)}'");
            }

            ResetConverters();
            return result;
        }
        finally
        {
            SEMAPHORE.Release();
        }
    }

    public static async Task BackupAndDelete(string path)
    {
        await SEMAPHORE.WaitAsync();
        try
        {
            var backupPath = $"{path}.backup";
            var json = await FileHelper.SafeReadStringAsync(path);
            if (json == null)
            {
                return;
            }
            await FileHelper.Delete(backupPath);
            await FileHelper.WriteAsync(backupPath, json);
            await FileHelper.Delete(path);            
        }
        finally
        {
            SEMAPHORE.Release();
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
