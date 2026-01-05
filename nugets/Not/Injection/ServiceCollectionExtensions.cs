using Microsoft.Extensions.DependencyInjection;

namespace Not.Injection;

public static class ServiceCollectionExtensions
{
    public static void Add(this IServiceCollection services, Type @interface, Type implementation, ServiceLifetime lifetime)
    {
        var service =
            @interface.IsGenericType && implementation.IsGenericType
                ? @interface.GetGenericTypeDefinition()
                : @interface;

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton(implementation);
                if (service != implementation)
                {
                    services.AddSingleton(service, x => x.GetRequiredService(implementation));
                }
                break;
            case ServiceLifetime.Scoped:
                services.AddScoped(implementation);
                if (service != implementation)
                {
                    services.AddScoped(service, x => x.GetRequiredService(implementation));
                }
                break;
            default:
                services.AddTransient(service, implementation);
                break;
        }
    }

    public static IServiceCollection Add<TInterface, TImplementation>(this IServiceCollection services, ServiceLifetime lifetime)
        where TImplementation : class, TInterface
        where TInterface : class
    {
        services.Add(typeof(TInterface), typeof(TImplementation), lifetime);
        return services;
    }

    public static IServiceCollection Add<TInterface1, TInterface2, TImplementation>(this IServiceCollection services, ServiceLifetime lifetime)
        where TImplementation : class, TInterface1, TInterface2
        where TInterface1 : class
        where TInterface2 : class
    {
        return services
            .Add<TInterface1, TImplementation>(lifetime)
            .Add<TInterface2, TImplementation>(lifetime);
    }

    public static IServiceCollection Add<TInterface1, TInterface2, TInterface3, TImplementation>(this IServiceCollection services, ServiceLifetime lifetime)
        where TImplementation : class, TInterface1, TInterface2, TInterface3
        where TInterface1 : class
        where TInterface2 : class
        where TInterface3 : class
    {
        return services
            .Add<TInterface1, TImplementation>(lifetime)
            .Add<TInterface2, TImplementation>(lifetime)
            .Add<TInterface3, TImplementation>(lifetime);
    }

    public static IServiceCollection Add<TInterface1, TInterface2, TInterface3, TInterface4, TImplementation>(
        this IServiceCollection services, 
        ServiceLifetime lifetime)
        where TImplementation : class, TInterface1, TInterface2, TInterface3, TInterface4
        where TInterface1 : class
        where TInterface2 : class
        where TInterface3 : class
        where TInterface4 : class
    {
        return services
            .Add<TInterface1, TImplementation>(lifetime)
            .Add<TInterface2, TImplementation>(lifetime)
            .Add<TInterface3, TImplementation>(lifetime)
            .Add<TInterface4, TImplementation>(lifetime);
    }

    static void PreventDuplicateRegistration(IServiceCollection services, Type @interface)
    {
        if (services.Any(x => x.ServiceType == @interface))
        {
            throw new ApplicationException($"Duplicate registration for service '{@interface.FullName}'");
        }
    }
}
