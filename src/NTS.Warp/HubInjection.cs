using Microsoft.AspNetCore.SignalR;
using Not.Injection;
using Not.Localization;
using Not.Serialization.JSON;
using NTS.Warp.Middlewares;

namespace NTS.Warp;

internal static class HubInjection
{
    public static IServiceCollection ConfigureWarp(this IServiceCollection services)
    {
        services
            .AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.AddFilter<ExceptionHandlingHubFilter>();
            })
            .AddNewtonsoftJsonProtocol(x => x.PayloadSerializerSettings = NJsonSettings.ConfigureServerSerialization());

        return services.AddDummyLocalizer().RegisterConventionalServices();
    }
}
