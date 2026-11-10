using System.Reflection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NC.Application.UdpHandshake;
using NC.Server.Authentication.Interface;
using NoTiming.Api.Application.Account;
using NoTiming.Application.Contracts.Account;
using NC.Server.Authentication;
using NoTiming.Api;
using NoTiming.Api.Application;
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
        services.AddSingleton<IProfileValidator<NoTimingProfile>, NoTimingProfileValidator>();
        services.AddNoTimingAuthentication<NoTimingProfile>(configuration, NoTimingCommonServices.SupportedLanguages);

        // The app sits behind the App Service front end, which appends the caller's address. Trust exactly one hop, so the
        // per-IP limit counts clients and cannot be spoofed by a client-supplied header.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });
        services.AddNoTimingApi();

        return services.AddTransient<INetworkBroadcastService, JudgeHandshakeService>();
    }
}
