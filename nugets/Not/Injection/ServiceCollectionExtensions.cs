using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Not.Injection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all public classes of <paramref name="type"/> and it's children. Separate registrations are created for every interface implemented
    /// </summary>
    /// <param name="services">ServiceCollection instance</param>
    /// <param name="type">Marker type to be registeded, including itself if not abstract</param>
    /// <param name="lifetime">Service lifetime</param>
    /// <param name="assembly">Assembly to scan for items of <paramref name="type"/></param>
    public static void AddAsInterfaces(
        this IServiceCollection services,
        Type type,
        ServiceLifetime lifetime,
        Assembly assembly
    )
    {
        services.Scan(config =>
            config
                .FromAssemblies(assembly)
                .AddClasses(classes => classes.AssignableTo(type))
                .AsImplementedInterfaces()
                .WithLifetime(lifetime)
        );
    }

    /// <summary>
    /// Registers all public classes of <paramref name="type"/> and it's children. Separate registrations are created for every interface implemented.
    /// Uses <seealso cref="Assembly.GetCallingAssembly" to obtain assembly to scan for <paramref name="type"/> />
    /// </summary>
    /// <param name="services">ServiceCollection instance</param>
    /// <param name="type">Marker type to be registeded, including itself if not abstract</param>
    /// <param name="lifetime">Service lifetime</param>
    public static void AddAsInterfaces(this IServiceCollection services, Type type, ServiceLifetime lifetime)
    {
        AddAsInterfaces(services, type, lifetime, Assembly.GetCallingAssembly());
    }

    /// <summary>
    /// Registers all public classes of <paramref name="type"/> and it's children. Separate registrations are created for every interface implemented.
    /// Uses <seealso cref="Assembly.GetCallingAssembly" to obtain assembly to scan for <paramref name="type"/> />
    /// </summary>
    /// <typeparam name="T">Marker type to be registeded, including itself if not abstract</typeparam>
    /// <param name="services">ServiceCollection instance</param>
    /// <param name="lifetime">Service lifetime</param>
    public static void AddAsInterfaces<T>(this IServiceCollection services, ServiceLifetime lifetime)
    {
        AddAsInterfaces(services, typeof(T), lifetime, Assembly.GetCallingAssembly());
    }

    public static void Add(
        this IServiceCollection services,
        Type @interface,
        Type implementation,
        ServiceLifetime lifetime
    )
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

    public static IServiceCollection Add<TInterface, TImplementation>(
        this IServiceCollection services,
        ServiceLifetime lifetime
    )
        where TImplementation : class, TInterface
        where TInterface : class
    {
        services.Add(typeof(TInterface), typeof(TImplementation), lifetime);
        return services;
    }

    public static IServiceCollection Add<TInterface1, TInterface2, TImplementation>(
        this IServiceCollection services,
        ServiceLifetime lifetime
    )
        where TImplementation : class, TInterface1, TInterface2
        where TInterface1 : class
        where TInterface2 : class
    {
        return services.Add<TInterface1, TImplementation>(lifetime).Add<TInterface2, TImplementation>(lifetime);
    }

    public static IServiceCollection Add<TInterface1, TInterface2, TInterface3, TImplementation>(
        this IServiceCollection services,
        ServiceLifetime lifetime
    )
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
        ServiceLifetime lifetime
    )
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

    public static IServiceCollection Add<
        TInterface1,
        TInterface2,
        TInterface3,
        TInterface4,
        TInterface5,
        TImplementation
    >(this IServiceCollection services, ServiceLifetime lifetime)
        where TImplementation : class, TInterface1, TInterface2, TInterface3, TInterface4, TInterface5
        where TInterface1 : class
        where TInterface2 : class
        where TInterface3 : class
        where TInterface4 : class
        where TInterface5 : class
    {
        return services
            .Add<TInterface1, TImplementation>(lifetime)
            .Add<TInterface2, TImplementation>(lifetime)
            .Add<TInterface3, TImplementation>(lifetime)
            .Add<TInterface4, TImplementation>(lifetime)
            .Add<TInterface5, TImplementation>(lifetime);
    }

    public static IServiceCollection Add<
        TInterface1,
        TInterface2,
        TInterface3,
        TInterface4,
        TInterface5,
        TInterface6,
        TImplementation
    >(this IServiceCollection services, ServiceLifetime lifetime)
        where TImplementation : class, TInterface1, TInterface2, TInterface3, TInterface4, TInterface5, TInterface6
        where TInterface1 : class
        where TInterface2 : class
        where TInterface3 : class
        where TInterface4 : class
        where TInterface5 : class
        where TInterface6 : class
    {
        return services
            .Add<TInterface1, TImplementation>(lifetime)
            .Add<TInterface2, TImplementation>(lifetime)
            .Add<TInterface3, TImplementation>(lifetime)
            .Add<TInterface4, TImplementation>(lifetime)
            .Add<TInterface5, TImplementation>(lifetime)
            .Add<TInterface6, TImplementation>(lifetime);
    }
}
