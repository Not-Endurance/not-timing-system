using Microsoft.Extensions.DependencyInjection;
using Not.Storage.Ports;

namespace Not.Storage.Stores;

public static class Extensions
{
    public static IServiceCollection AddJsonFileStore<T>(this IServiceCollection services)
        where T : class, IFileStorageConfiguration, new()
    {
        services.AddSingleton<IFileStorageConfiguration, T>();
        services.AddSingleton(typeof(IStore<>), typeof(JsonFileStore<>));
        return services;
    }
}