using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using NC.Serialization.JSON;
using NoTiming.EventBus.Features.Judge;
using NoTiming.EventBus.Features.Witness;
using NoTiming.EventBus.Middlewares;
using NoTiming.Application.Contracts;

namespace NoTiming.EventBus;

public static class NoTimingEventBusServices
{
    public static IServiceCollection AddNoTimingEventBus(this IServiceCollection services)
    {
        services
            .AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.MaximumReceiveMessageSize = 1024 * 1024;
                options.AddFilter<ExceptionHandlingHubFilter>();
            })
            .AddNewtonsoftJsonProtocol(x => x.PayloadSerializerSettings = NJsonSettings.ConfigureServerSerialization());

        return services;
    }

    public static void MapNoTimingEventBus(this IEndpointRouteBuilder endpoints, string corsPolicyName)
    {
        endpoints
            .MapHub<JudgeRpcHub>(ApplicationConstants.EventBusHub(ApplicationConstants.JUDGE_HUB))
            .RequireCors(corsPolicyName);
        endpoints
            .MapHub<WitnessRpcHub>(ApplicationConstants.EventBusHub(ApplicationConstants.WITNESS_HUB))
            .RequireCors(corsPolicyName);
    }
}
