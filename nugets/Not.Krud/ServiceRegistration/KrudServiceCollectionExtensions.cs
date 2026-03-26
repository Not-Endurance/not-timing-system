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
        services.AddTransient(typeof(IRepository<>), typeof(KrudInMemoryNodeRepository<>));
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

        var meta = KrudGraphMetadata.Build(typeof(T));
        RegisterMirrors<T>(services, meta, lifetime);

        if (meta.IsFlatAggregate)
        {
            return services;
        }

        services.Add(new(typeof(KrudGraphContext<T>), typeof(KrudGraphContext<T>), lifetime));
        services.Add(new(typeof(IKrudNodeSetter), sp => sp.GetRequiredService<KrudGraphContext<T>>(), lifetime));
        services.Add(
            new(typeof(IKrudDependencyResolver), sp => sp.GetRequiredService<KrudGraphContext<T>>(), lifetime)
        );

        foreach (var @interface in meta.KrudParentNodeOfClosedInterfaces)
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
