using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.UdpHandshake;
using Not.Server.Authentication;
using NoTiming.Api;
using NoTiming.Api.Application;
using NoTiming.EventBus;
using NoTiming.Host.Cors;
using NTS;
using NTS.Application;

namespace NoTiming;

internal static class NoTimingServices
{
    public const string CORS_POLICY_NAME = "NoTimingCors";

    public static IServiceCollection ConfigureNoTiming(this IServiceCollection services, IConfiguration configuration)
    {
        var originValidator = services.AddNtsCorsOriginValidation(configuration);

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
        services.ConfigureNtsApplication(configuration, Assembly.GetCallingAssembly());
        services.AddNts(configuration);
        services.AddNoTimingApiApplication(configuration);
        services.NJwtTokenValidation(configuration);
        services.AddNoTimingApi();

        return services.AddTransient<INetworkBroadcastService, JudgeHandshakeService>();
    }
}
