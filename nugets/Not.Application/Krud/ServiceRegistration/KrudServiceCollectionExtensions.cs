using Microsoft.Extensions.DependencyInjection;
using Not.Application.CRUD.Ports;
using Not.Application.Krud.Abstractions;
using Not.Application.Krud.Graph;
using Not.Application.Krud.Services;
using Not.Domain.Aggregates;

namespace Not.Application.Krud.ServiceRegistration;

public static class KrudServiceCollectionExtensions
{
    public static KrudBuilder ConfigureKrud(this IServiceCollection services)
    {
        services.AddTransient(typeof(IRepository<>), typeof(KrudInMemoryNodeRepository<>));
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
        ServiceLifetime lifetime = ServiceLifetime.Singleton
    )
        where T : AggregateRoot
    {
        if (lifetime == ServiceLifetime.Transient)
        {
            throw new InvalidOperationException(
                $"Krud aggregates cannot be with Transient lifetime. Aggregate '{typeof(T).FullName}'"
            );
        }

        var meta = KrudGraphMetadata.Build(typeof(T));
        if (meta.IsFlatAggregate)
        {
            return services;
        }

        services.Add(new(typeof(KrudGraphContext<T>), typeof(KrudGraphContext<T>), lifetime));
        services.Add(new(typeof(IKrudNodeSetter), sp => sp.GetRequiredService<KrudGraphContext<T>>(), lifetime));

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
}
