using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using Not.Application.UdpHandshake;
using Not.Localization;
using Not.Serialization.JSON;
using NTS.Application;
using NTS.Warp.Middlewares;

namespace NTS.Warp;

internal static class NtsWarpServices
{
    public static IServiceCollection ConfigureNtsWarp(this IServiceCollection services, IConfiguration configuration)
    {
        var corsSettings = configuration.GetSection(nameof(CorsSettings)).Get<CorsSettings>() ?? new CorsSettings();
        var originValidator = new CorsOriginValidator(corsSettings);
        services.AddSingleton(originValidator);

        services.AddCors(options =>
            options.AddPolicy(
                CorsSettings.POLICY_NAME,
                policy =>
                    policy
                        .SetIsOriginAllowed(originValidator.IsAllowed)
                        .WithMethods("GET", "POST")
                        .AllowAnyHeader()
                        .AllowCredentials()
            )
        );

        services
            .AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.MaximumReceiveMessageSize = 1024 * 1024;
                options.AddFilter<ExceptionHandlingHubFilter>();
            })
            .AddNewtonsoftJsonProtocol(x => x.PayloadSerializerSettings = NJsonSettings.ConfigureServerSerialization());

        // TODO: Not.Application is getting handshaked..

        services.ConfigureNtsApplication(configuration, Assembly.GetCallingAssembly());
        return services.AddDummyLocalizer().AddTransient<INetworkBroadcastService, JudgeHandshakeService>();
    }
}
