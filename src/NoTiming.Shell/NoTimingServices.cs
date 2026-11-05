using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NC.Application.UdpHandshake;
using NoTiming.Api;
using NoTiming.Api.Application;
using NoTiming.Api.Application.Authentication;
using NoTiming.Application;
using NoTiming.EventBus;
using NoTiming.Host.Cors;
using NoTiming.Shared;

namespace NoTiming;

internal static class NoTimingServices
{
    public const string CORS_POLICY_NAME = "NoTimingCors";

    public static IServiceCollection ConfigureNoTiming(this IServiceCollection services, IConfiguration configuration)
    {
        var originValidator = services.AddNoTimingCorsOriginValidation(configuration);

        services.AddCors(options =>
            options.AddPolicy(
                CORS_POLICY_NAME,
                policy =>
                    policy
                        .SetIsOriginAllowed(originValidator.IsAllowed)
                        .WithMethods("GET", "POST", "PATCH", "DELETE")
                        .AllowAnyHeader()
                        .AllowCredentials()
            )
        );

        services.AddNoTimingEventBus();
        services.ConfigureNoTimingApplication(configuration, Assembly.GetCallingAssembly());
        services.AddNoTiming(configuration);
        services.AddNoTimingApiApplication(configuration);
        services.AddNoTimingAuthentication(configuration);
        services.AddNoTimingApi();

        return services.AddTransient<INetworkBroadcastService, JudgeHandshakeService>();
    }
}
