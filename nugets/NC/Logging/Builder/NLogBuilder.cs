using Microsoft.Extensions.DependencyInjection;

namespace NC.Logging.Builder;

public class NLogBuilder
{
    public const string KEY = "NLogs";

    public NLogBuilder(IServiceCollection services)
    {
        Services = services;
    }

    public IServiceCollection Services { get; }
}
