using Microsoft.Extensions.DependencyInjection;
using Not.Domain.Ports;
using Not.Filesystem;
using Not.Storage.Stores.Files;

namespace Not.Storage.Stores.StaticOptions;

public class JsonStaticOptionsProvider<T> : JsonFileStore<T>, IStaticOptionsProvider<T>
    where T : class, new()
{
    public JsonStaticOptionsProvider([FromKeyedServices(StoreConstants.STATIC_OPTIONS_STORE_KEY)] IFileContext context)
        : base(Path.Combine(context.Path, "static-options.json")) { }

    public T Get()
    {
        return Deserialize();
    }
}
