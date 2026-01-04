using Microsoft.Extensions.DependencyInjection;
using Not.Filesystem;
using Not.Storage.Stores.Files;

namespace Not.Storage.Stores;

public static class StoreServiceCollectionExtensions
{
    /// <summary>
    /// FileContext defaults to <seealso cref="FileContextHelper.GetAppDirectory(string)"/> using 'stores' as subdirectory
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configure">Custom configuration</param>
    /// <returns></returns>
    public static IServiceCollection AddJsonFileStore(
        this IServiceCollection services,
        Action<FileContext>? configure = null
    )
    {
        var factory = FileContextHelper.CreateFileContextFactory(configure, "stores");
        services.AddKeyedSingleton<IFileContext, FileContext>(StoreConstants.DATA_KEY, factory);
        services.AddSingleton(typeof(IStore<>), typeof(LockingJsonFileStore<>));
        return services;
    }
}
