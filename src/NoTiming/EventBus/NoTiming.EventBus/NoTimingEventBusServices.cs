using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NC.Serialization.JSON;
using NC.Storage.Mongo;
using NoTiming.Application.Contracts;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Application.Contracts.Notices;
using NoTiming.Application.Contracts.Shared;
using NoTiming.EventBus.Features.Judge;
using NoTiming.EventBus.Features.Witness;
using NoTiming.EventBus.Middlewares;
using NoTiming.EventBus.Outbox;

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

        services.AddSingleton<ParticipationChangedPublisher>();
        services.AddSingleton<IHostedService>(sp => new OutboxPoller<ParticipationModel, ParticipationChangedNotice>(
            sp.GetRequiredService<IMongoContext>(),
            StorageNames.NTS_DATABASE,
            StorageNames.PARTICIPATIONS_COLLECTION,
            sp.GetRequiredService<ParticipationChangedPublisher>().Publish,
            sp.GetRequiredService<ILogger<OutboxPoller<ParticipationModel, ParticipationChangedNotice>>>()
        ));

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
