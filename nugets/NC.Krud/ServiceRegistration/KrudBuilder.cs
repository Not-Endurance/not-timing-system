using Microsoft.Extensions.DependencyInjection;
using NC.Domain;

namespace NC.Krud.ServiceRegistration;

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
