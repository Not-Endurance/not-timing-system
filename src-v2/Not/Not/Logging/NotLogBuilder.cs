using Microsoft.Extensions.DependencyInjection;

namespace Not.Logging;

public class NotLogBuilder
{
    public NotLogBuilder(IServiceCollection services)
    {
        Services = services;
    }

    public IServiceCollection Services { get; }
}
