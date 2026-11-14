using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NC.Application.Authentication.User;
using NC.Storage;
using NoTiming.Api.Application.Features.Ui.Authorization;
using NoTiming.Api.Application.Mongo.Repositories;
using NoTiming.Application.Mongo;

namespace NoTiming.Api.Application;

public static class NoTimingApiApplicationServices
{
    public static IServiceCollection AddNoTimingApiApplication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddPendingSnapshotsMongoStorage(configuration);
        services.AddSingleton<IReceiveSnapshotAccessPolicy, MongoUiReceiveAccessPolicy>();
        services.AddSingleton<IUiReceiveAuthorizer, UiReceiveAuthorizer>();
        services.AddTransient<IUserRepository, UserMongoRepository>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<NoTiming.Api.Application.Event.EventFinalizationJob>();

        return services;
    }

    static void AddPendingSnapshotsMongoStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["MONGO_CONNECTION_STRING"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ApplicationException(
                "MongoDB connection string is missing. Set MONGO_CONNECTION_STRING in configuration or user secrets."
            );
        }

        var builder = new NStorageBuilder(services, configuration);
        NoTimingMongoSerialization.Configure();
        builder.AddMongoStorage(connectionString, Assembly.GetExecutingAssembly());
    }
}
