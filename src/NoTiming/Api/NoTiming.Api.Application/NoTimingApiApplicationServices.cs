using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NC.Application.Authentication.User;
using NC.Storage;
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
        services.AddPendingSnapshotsMongoStorage(configuration);
        services.AddSingleton<IReceiveSnapshotAccessPolicy, MongoUiReceiveAccessPolicy>();
        services.AddSingleton<IUiReceiveAuthorizer, UiReceiveAuthorizer>();
        services.AddTransient<IUserRepository, UserMongoRepository>();

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
        builder.AddMongoStorage(connectionString, Assembly.GetExecutingAssembly());
    }
}
