using Microsoft.Extensions.DependencyInjection;
using NC.Logging.Builder;
using Serilog;

namespace NC.Logging;

public static class LoggingServiceCollectionExtensions
{
    public static NLogBuilder AddNLogging(this IServiceCollection services)
    {
        services.AddSerilog();
        return new NLogBuilder(services);
    }
}
