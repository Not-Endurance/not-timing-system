using Microsoft.Extensions.DependencyInjection;
using Not.Exceptions;
using Not.Startup;

namespace Not.Injection;

public class ServiceLocator : IStartupInitializer, ITransient
{
    public static T GetRequired<T>()
        where T : class
    {
        GuardHelper.ThrowIfDefault(_provider);
        return _provider.GetRequiredService<T>();
    }

    public static T? Get<T>()
        where T : class
    {
        return _provider?.GetService<T>();
    }

    static IServiceProvider? _provider;

    public ServiceLocator(IServiceProvider provider)
    {
        _provider ??= provider;
    }

    public void RunAtStartup()
    {
        // It's just necessary to load ServiceLocator in order to set _provider from DI container
    }
}
