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
            throw GuardHelper.Exception(
                $"{nameof(ServiceLocator)} cannot guarantee correct service scope resolution. Scoped service '{typeof(T).Name}' is not allowed"
            );
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
