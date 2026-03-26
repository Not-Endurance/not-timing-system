using Microsoft.Extensions.DependencyInjection;
using Not.Exceptions;
using Not.Startup;

namespace Not.Injection;

public class ServiceLocator : IStartupInitializer, ITransient
{
    public static T? Get<T>()
        where T : class
    {
        var service = _provider?.GetService<T>();
        if (service is IScoped)
        {
            throw GuardHelper.Exception($"Cannot resolve scoped service '{typeof(T).Name}' via {nameof(ServiceLocator)}.");
        }

        return service;
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
