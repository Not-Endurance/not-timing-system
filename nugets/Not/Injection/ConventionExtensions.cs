using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Not.Injection;

public static class InjectionServiceCollectionExtensions
{
    const string NOT_PREFIX = "Not.";
    static readonly Type TRANSIENT_TYPE = typeof(ITransient);
    static readonly Type SCOPED_TYPE = typeof(IScoped);
    static readonly Type SINGLETON_TYPE = typeof(ISingleton);

    public static IServiceCollection AddNConventionalServices(this IServiceCollection services, Assembly rootAssembly)
    {
        var assemblies = rootAssembly.RecursiveGetReferencedAssemblies([]);
        return RegisterNConventionalServices(services, assemblies);
    }

    static IServiceCollection RegisterNConventionalServices(
        IServiceCollection services,
        IEnumerable<Assembly> assemblies
    )
    {
        assemblies = assemblies.Distinct().OrderBy(x => x.FullName).ToList();
        var classes = assemblies
            .SelectMany(x => x.GetTypes().Where(y => !y.IsInterface && !y.IsAbstract && y.IsConventionalService()))
            .ToList();
        foreach (var implementation in classes)
        {
            RegisterImplemenationByConvention(implementation, services);
        }
        return services;
    }

    static void RegisterImplemenationByConvention(Type implementation, IServiceCollection services)
    {
        var interfaces = implementation
            .GetInterfaces()
            .Where(x =>
                x.IsAssignableFrom(implementation) && x != TRANSIENT_TYPE && x != SCOPED_TYPE && x != SINGLETON_TYPE
            )
            .ToList();

        if (interfaces.Any(x => x.IsSingleton()))
        {
            AddAsSelfWithInterfaces(services, interfaces, implementation, ServiceLifetime.Singleton);
            return;
        }
        if (interfaces.Any(x => x.IsScoped()))
        {
            AddAsSelfWithInterfaces(services, interfaces, implementation, ServiceLifetime.Scoped);
            return;
        }
        foreach (var i in interfaces)
        {
            Add(services, i, implementation, ServiceLifetime.Transient);
        }
    }

    static void AddAsSelfWithInterfaces(
        IServiceCollection services,
        IEnumerable<Type> interfaces,
        Type implementation,
        ServiceLifetime lifetime
    )
    {
        PreventInvalidPolymorphicService(implementation);

        // Register as self and use self to fetch the instace for all interfaces
        Add(services, implementation, implementation, lifetime);
        foreach (var @interface in interfaces)
        {
            var descriptor = new ServiceDescriptor(@interface, x => x.GetRequiredService(implementation), lifetime);
            services.Add(descriptor);
        }
    }

    static void Add(IServiceCollection services, Type @interface, Type implementation, ServiceLifetime lifetime)
    {
        var service =
            @interface.IsGenericType && implementation.IsGenericType
                ? @interface.GetGenericTypeDefinition()
                : @interface;

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton(service, implementation);
                break;
            case ServiceLifetime.Scoped:
                services.AddScoped(service, implementation);
                break;
            default:
                services.AddTransient(service, implementation);
                break;
        }
    }

    static void PreventInvalidPolymorphicService(Type implementation)
    {
        if (
            implementation.BaseType != null
            && !implementation.BaseType.IsAbstract
            && (
                implementation.IsSingleton() && implementation.BaseType.IsSingleton()
                || implementation.IsScoped() && implementation.BaseType.IsScoped()
            )
        )
        {
            var message = $"""
                '{implementation.Name}' and it's parent '{implementation.BaseType.Name}' cannot be
                instantiable NonTransient services. Either declare base class as 'abstract' or decouple.
                This check exists to prevent accidental invokations of the base class instead of the derrived
                or to prevent unwanted duplications in case of IEnumerable<T> injection
                """;
            throw new Exception(message);
        }
    }

    static Assembly[] RecursiveGetReferencedAssemblies(this Assembly assembly, List<Assembly> result)
    {
        if (result.Any(x => x.FullName == assembly.FullName))
        {
            return result.ToArray();
        }
        result.Add(assembly);
        var ntsPrefix = assembly.FullName!.Split('.').First();
        var references = assembly
            .GetReferencedAssemblies()
            .Where(x =>
                x.FullName!.StartsWith(ntsPrefix, StringComparison.InvariantCulture)
                || x.FullName!.StartsWith(NOT_PREFIX, StringComparison.InvariantCulture)
                || x.FullName!.StartsWith("NTS.", StringComparison.InvariantCulture)
            ) // TODO: remove
            .ToList();
        foreach (var reference in references)
        {
            var innerAssembly = Assembly.Load(reference);
            RecursiveGetReferencedAssemblies(innerAssembly, result);
        }
        return result.ToArray();
    }

    static bool IsConventionalService(this Type type)
    {
        return type.IsTransient() || type.IsScoped() || type.IsSingleton();
    }

    static bool IsTransient(this Type type)
    {
        return type.Name != TRANSIENT_TYPE.Name && TRANSIENT_TYPE.IsAssignableFrom(type);
    }

    static bool IsScoped(this Type type)
    {
        return type.Name != SCOPED_TYPE.Name && SCOPED_TYPE.IsAssignableFrom(type);
    }

    static bool IsSingleton(this Type type)
    {
        return type.Name != SINGLETON_TYPE.Name && SINGLETON_TYPE.IsAssignableFrom(type);
    }
}
