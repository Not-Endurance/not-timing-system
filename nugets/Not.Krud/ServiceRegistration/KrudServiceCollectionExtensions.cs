using Microsoft.Extensions.DependencyInjection;
using Not.Application.CRUD.Ports;
using Not.Domain;
using Not.Krud.Abstractions;
using Not.Krud.Blazor;
using Not.Krud.Graph;
using Not.Krud.Services;

namespace Not.Krud.ServiceRegistration;

public static class KrudServiceCollectionExtensions
{
    public static KrudBuilder ConfigureKrud(this IServiceCollection services)
    {
        services.AddTransient(typeof(KrudDialogService<,>));
        return new(services);
    }

    /// <summary>
    /// Registers an Aggregate in the Krud framework. If the aggregate has child entities this method builds a dependency graph
    /// and registers individual nodes for every Parent entity that hold the state of the current entity. This state is then used
    /// by <seealso cref="KrudInMemoryNodeRepository{T}"/> in order to Provide simple CRUD interface for child entities
    /// </summary>
    /// <typeparam name="T">Type of the Domain Aggregate to register</typeparam>
    /// <param name="services">Application's serivce collection</param>
    /// <param name="lifetime">Service lifetime</param>
    /// <returns></returns>
    internal static IServiceCollection AddKrudRoot<T>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped
    )
        where T : Aggregate
    {
        if (lifetime == ServiceLifetime.Transient)
        {
            throw new InvalidOperationException(
                $"Krud aggregates cannot be with Transient lifetime. Aggregate '{typeof(T).FullName}'"
            );
        }

        var metadata = KrudGraphMetadata.Build(typeof(T));
        RegisterMirrors<T>(services, metadata, lifetime);

        if (metadata.IsFlatAggregate)
        {
            return services;
        }

        services.Add(new(typeof(KrudGraphContext<T>), typeof(KrudGraphContext<T>), lifetime));
        RegisterKrudNodeRepositories(services, metadata, lifetime);
        services.Add(new(typeof(IKrudNodeSetter), sp => sp.GetRequiredService<KrudGraphContext<T>>(), lifetime));
        services.Add(
            new(typeof(IKrudDependencyResolver), sp => sp.GetRequiredService<KrudGraphContext<T>>(), lifetime)
        );

        foreach (var @interface in metadata.KrudParentNodeOfClosedInterfaces)
        {
            services.Add(
                new(
                    @interface,
                    sp => sp.GetRequiredService<KrudGraphContext<T>>().GetNodeByClosedParentInterface(@interface),
                    lifetime
                )
            );
        }

        services.Add(
            new(
                typeof(IEnumerable<IKrudNodeSetter>),
                sp => sp.GetRequiredService<KrudGraphContext<T>>().GetNodeSetters(),
                lifetime
            )
        );

        return services;
    }

    static void RegisterKrudNodeRepositories(
        IServiceCollection services,
        KrudGraphMetadata meta,
        ServiceLifetime lifetime
    )
    {
        var registrations = meta
            .KrudParentNodeOfClosedInterfaces.Select(parentNodeInterface => new
            {
                ChildType = parentNodeInterface.GetGenericArguments()[0],
                ParentNodeInterface = parentNodeInterface,
            })
            .GroupBy(x => x.ChildType);

        foreach (var registration in registrations)
        {
            var closedRegistrations = registration.ToList();
            if (closedRegistrations.Count > 1)
            {
                var parentNodes = string.Join(", ", closedRegistrations.Select(x => x.ParentNodeInterface.FullName));
                throw new InvalidOperationException(
                    $"Cannot register Krud in-memory repository for '{registration.Key.FullName}' because multiple parent nodes were found: {parentNodes}"
                );
            }

            RegisterKrudNodeRepository(
                services,
                closedRegistrations[0].ChildType,
                closedRegistrations[0].ParentNodeInterface,
                lifetime
            );
        }
    }

    static void RegisterKrudNodeRepository(
        IServiceCollection services,
        Type childType,
        Type parentNodeInterface,
        ServiceLifetime lifetime
    )
    {
        var serviceType = typeof(IRepository<>).MakeGenericType(childType);
        var repositoryType = typeof(KrudInMemoryNodeRepository<>).MakeGenericType(childType);

        EnsureNoExistingRepositoryRegistration(services, serviceType, childType);

        services.Add(
            new ServiceDescriptor(
                serviceType,
                sp =>
                {
                    var parentNode = sp.GetRequiredService(parentNodeInterface);
                    var resolvers = sp.GetServices<IKrudDependencyResolver>();
                    return ActivatorUtilities.CreateInstance(sp, repositoryType, parentNode, resolvers);
                },
                lifetime
            )
        );
    }

    static void EnsureNoExistingRepositoryRegistration(IServiceCollection services, Type serviceType, Type childType)
    {
        var existing = services.Where(x => x.ServiceType == serviceType).Select(Describe).ToList();
        if (!existing.Any())
        {
            return;
        }

        throw new InvalidOperationException(
            $"Cannot register Krud in-memory repository for '{childType.FullName}' because '{serviceType.FullName}' is already registered: {string.Join("; ", existing)}"
        );
    }

    static string Describe(ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationType != null)
        {
            return descriptor.ImplementationType.FullName ?? descriptor.ImplementationType.Name;
        }

        if (descriptor.ImplementationInstance != null)
        {
            return descriptor.ImplementationInstance.GetType().FullName
                ?? descriptor.ImplementationInstance.GetType().Name;
        }

        return descriptor.ImplementationFactory == null ? "<unknown>" : "<factory>";
    }

    static void RegisterMirrors<T>(IServiceCollection services, KrudGraphMetadata meta, ServiceLifetime lifetime)
        where T : Aggregate
    {
        foreach (var principalType in meta.RootMirrorPrincipalTypes)
        {
            services.Add(
                new(
                    typeof(IKrudMirrorService<>).MakeGenericType(principalType),
                    typeof(KrudRootMirror<,>).MakeGenericType(typeof(T), principalType),
                    lifetime
                )
            );
        }

        foreach (var principalType in meta.GraphMirrorPrincipalTypes)
        {
            services.Add(
                new(
                    typeof(IKrudMirrorService<>).MakeGenericType(principalType),
                    typeof(KrudGraphMirror<,>).MakeGenericType(typeof(T), principalType),
                    lifetime
                )
            );
        }
    }
}
