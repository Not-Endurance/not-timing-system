using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NC.Application.Authentication.User;
using NC.Server.ClientIdentity;
using NC.Storage;
using NoTiming.Api.Application.Features.Participations;
using NoTiming.Api.Application.Features.Snapshots;
using NoTiming.Api.Application.Features.Ui.Authorization;
using NoTiming.Api.Application.Mongo.Repositories;

namespace NoTiming.Api.Application;

public static class NoTimingApiApplicationServices
{
    public static IServiceCollection AddNoTimingApiApplication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddMongoStorageRepositories(configuration);
        services.AddNRequestClientIdentity();
        services.AddSingleton<IReceiveSnapshotAccessPolicy, MongoUiReceiveAccessPolicy>();
        services.AddSingleton<SnapshotAuthorizer>();
        services.AddSingleton<EventWriteLocks>();
        services.AddTransient<IParticipationChangeRepository, ParticipationEventScopedMongoRepository>();
        services.AddScoped<ParticipationCommandService>();
        services.AddScoped<SnapshotApplicationService>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<NoTiming.Api.Application.Event.EventFinalizationJob>();

        return services;
    }

    static void AddMongoStorageRepositories(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["MONGO_CONNECTION_STRING"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ApplicationException(
                "MongoDB connection string is missing. Set MONGO_CONNECTION_STRING in configuration or user secrets."
            );
        }

        var builder = new NStorageBuilder(services, configuration);
        builder.AddMongoStorage(connectionString, Assembly.GetExecutingAssembly());
    }
}
