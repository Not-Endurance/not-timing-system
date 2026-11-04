using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using Not.Application.Authentication.User;
using Not.Application.UdpHandshake;
using Not.Serialization.JSON;
using Not.Server.Authentication;
using Not.Storage;
using NTS;
using NTS.Application;
using NTS.Application.Mongo;
using NoTiming.Api.Errors;
using NoTiming.Api.Mongo.Repositories;
using NoTiming.Api.Telemetry;
using NoTiming.Features.Witness.Authorization;
using NoTiming.Host.Cors;
using NoTiming.Middlewares;

namespace NoTiming;

internal static class NtsWarpServices
{
    public const string CORS_POLICY_NAME = "NtsWarpCors";

    public static IServiceCollection ConfigureNtsWarp(this IServiceCollection services, IConfiguration configuration)
    {
        var originValidator = services.AddNtsCorsOriginValidation(configuration);

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

        services
            .AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.MaximumReceiveMessageSize = 1024 * 1024;
                options.AddFilter<ExceptionHandlingHubFilter>();
            })
            .AddNewtonsoftJsonProtocol(x => x.PayloadSerializerSettings = NJsonSettings.ConfigureServerSerialization());

        services.ConfigureNtsApplication(configuration, Assembly.GetCallingAssembly());
        services.AddNts(configuration);
        services.AddPendingSnapshotsMongoStorage(configuration);
        services.NJwtTokenValidation(configuration);
        services.AddSingleton<IReceiveSnapshotAccessPolicy, MongoWitnessWriteAccessPolicy>();
        services.AddSingleton<IWitnessReceiveAuthorizer, WitnessReceiveAuthorizer>();
        services.AddSingleton<ITelemetryService, TelemetryService>();
        services.AddTransient<IUserRepository, UserMongoRepository>();
        services.AddControllers(options => options.Filters.Add<ApiExceptionFilter>());

        return services.AddTransient<INetworkBroadcastService, JudgeHandshakeService>();
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
        NtsMongoSerialization.Configure();
        builder.AddMongoStorage(connectionString, Assembly.GetExecutingAssembly());
    }
}
