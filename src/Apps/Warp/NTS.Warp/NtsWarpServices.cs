using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using Not.Application.UdpHandshake;
using Not.Injection;
using Not.Localization;
using Not.Serialization.JSON;
using NTS.Warp.Middlewares;

namespace NTS.Warp;

internal static class NtsWarpServices
{
    public static IServiceCollection ConfigureNtsWarp(this IServiceCollection services, IConfiguration _)
    {
        services
            .AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.MaximumReceiveMessageSize = 1024 * 1024;
                options.AddFilter<ExceptionHandlingHubFilter>();
            })
            .AddNewtonsoftJsonProtocol(x => x.PayloadSerializerSettings = NJsonSettings.ConfigureServerSerialization());

        // TODO: Not.Application is getting handshaked..
        return services
            .AddNConventionalServices(Assembly.GetExecutingAssembly())
            .AddDummyLocalizer()
            .AddTransient<INetworkBroadcastService, JudgeHandshakeService>();
    }
}
