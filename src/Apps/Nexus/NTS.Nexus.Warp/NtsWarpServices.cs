using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using Not.Application.Authentication.User;
using Not.Application.UdpHandshake;
using Not.Serialization.JSON;
using Not.Server.Authentication;
using Not.Storage;
using NTS.Application;
using NTS.Application.Mongo;
using NTS.Nexus.Warp.Api.Errors;
using NTS.Nexus.Warp.Api.Mongo.Repositories;
using NTS.Nexus.Warp.Api.Telemetry;
using NTS.Nexus.Warp.Features.Witness.Authorization;
using NTS.Nexus.Warp.Host.Cors;
using NTS.Nexus.Warp.Middlewares;

namespace NTS.Nexus.Warp;

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
        var connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ApplicationException("MongoDB connection string is null");
        }

        var builder = new NStorageBuilder(services, configuration);
        NtsMongoSerialization.Configure();
        builder.AddMongoStorage(connectionString, Assembly.GetExecutingAssembly());
    }
}
