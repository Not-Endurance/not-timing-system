using Microsoft.Extensions.DependencyInjection;
using Not.Application.CRUD.Ports;
using Not.Application.Krud.Abstractions;
using Not.Application.Krud.Services;
using Not.Domain.Aggregates;
using Not.Startup;

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
   /// and registers individual nodes for every Parent entity that hold the state of the current entity. This tate is then used 
   /// by <seealso cref="KrudInMemoryNodeRepository{T}"/> in order to Provide simple CRUD interface for child entities
   /// </summary>
   /// <typeparam name="T">Type of the Domain Aggregate to register</typeparam>
   /// <param name="services">Application's serivce collection</param>
   /// <param name="lifetime">Service lifetime</param>
   /// <returns></returns>
    internal static IServiceCollection AddKrudRoot<T>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where T : AggregateRoot
    {
        var rootType = typeof(T);
        var graph = KrudGraphHelper.Build(rootType);

        if (!graph.AllNodes.Any())
        {
            return services;
        }

        services.Add(new ServiceDescriptor(typeof(IStartupInitializer), typeof(KrudGraphContext<T>), lifetime));

        foreach (var node in graph.AllNodes)
        {
            if (node.IsRoot)
            {
                var root = new KrudRootProvider<T>();
                root.SetRootNode(node);
                services.Add(new ServiceDescriptor(typeof(KrudRootProvider<T>), _ => root, lifetime));
                services.Add(new ServiceDescriptor(typeof(IKrudNodeSetter), _ => root, lifetime));
            }

            services.Add(new ServiceDescriptor(node.GetType(), _ => node, lifetime));
            if (node is IKrudNodeSetter setter)
            {
                services.Add(new ServiceDescriptor(typeof(IKrudNodeSetter), _ => setter, lifetime));
            }
            foreach (var iface in KrudReflectionHelper.GetClosedKrudParentInterfaces(node.GetType()))
            {
                services.Add(new ServiceDescriptor(iface, _ => node, lifetime));
            }
        }
        return services;
    }
}
