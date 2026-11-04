using Microsoft.Extensions.DependencyInjection;
using NoTiming.Api.Application.Telemetry;
using NoTiming.Api.Errors;

namespace NoTiming.Api;

public static class NoTimingApiServices
{
    public static IServiceCollection AddNoTimingApi(this IServiceCollection services)
    {
        services.AddSingleton<ITelemetryService, TelemetryService>();
        services.AddControllers(options => options.Filters.Add<ApiExceptionFilter>());

        return services;
    }
}
