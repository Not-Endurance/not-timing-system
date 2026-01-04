using Microsoft.Extensions.DependencyInjection;
using Not.Logging.Builder;
using Serilog;

namespace Not.Logging;

public static class LoggingServiceCollectionExtensions
{
    public static NLogBuilder AddNLogging(this IServiceCollection services)
    {
        services.AddSerilog();
        return new NLogBuilder(services);
    }
}
