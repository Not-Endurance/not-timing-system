using Not.Filesystem;
using Not.Serialization;

namespace Not.Storage.Stores.Files;

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
        var contents = value.ToConvertedJson();
        FileHelper.Write(contents, _path);
    }

    protected T Deserialize()
    {
        var contents = FileHelper.SafeReadString(_path);
        return contents?.FromConvertedJson<T>() ?? new();
    }

    protected async Task SerializeAsync(T value)
    {
        var contents = value.ToConvertedJson();
        await FileHelper.WriteAsync(contents, _path);
    }

    protected async Task<T> DeserializeAsync()
    {
        var contents = await FileHelper.SafeReadStringAsync(_path);
        return contents?.FromConvertedJson<T>() ?? new();
    }
}
