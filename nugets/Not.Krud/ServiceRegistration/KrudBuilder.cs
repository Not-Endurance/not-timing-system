using Microsoft.Extensions.DependencyInjection;
using Not.Domain;

namespace Not.Krud.ServiceRegistration;

public class KrudBuilder
{
    readonly IServiceCollection _services;

    public KrudBuilder(IServiceCollection services)
    {
        _services = services;
    }

    public KrudBuilder RegisterAggregate<T>()
        where T : Aggregate
    {
        _services.AddKrudRoot<T>();
        return this;
    }
}
