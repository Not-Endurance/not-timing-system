using Microsoft.AspNetCore.SignalR;
using Not.Injection;
using Not.Localization;
using Not.Serialization.JSON;
using NTS.Application.Handshake;
using NTS.Relay.Middleware;
using NTS.Storage;

namespace NTS.Relay;

internal static class HubInjection
{
    public static IServiceCollection ConfigureHub(this IServiceCollection services)
    {
        services
            .AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.AddFilter<ExceptionHandlingHubFilter>();
            })
            .AddNewtonsoftJsonProtocol(x =>
            {
                x.PayloadSerializerSettings = new NJsonSettings();
            });

        services
            .AddHostedService<NetworkBroadcastService>()
            .AddDummyLocalizer()
            .RegisterConventionalServices()
            .ConfigureStorage();

        return services;
    }
}
