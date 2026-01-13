using Microsoft.Extensions.DependencyInjection;
using Not.Domain.Aggregates;

namespace Not.Application.Krud.ServiceRegistration;

public class KrudBuilder
{
    private readonly IServiceCollection _services;

    public KrudBuilder(IServiceCollection services)
    {
        _services = services;
    }

    public KrudBuilder RegisterAggregate<T>()
        where T : AggregateRoot
    {
        _services.AddKrudRoot<T>();
        return this;
    }
}
