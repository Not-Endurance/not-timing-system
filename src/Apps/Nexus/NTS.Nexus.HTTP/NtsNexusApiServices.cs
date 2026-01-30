using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Injection;
using Not.Storage;

namespace NTS.Nexus.HTTP;

internal static class NtsNexusApiServices
{
    public static IServiceCollection ConfigureNexusApi(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddNConventionalServices(Assembly.GetExecutingAssembly())
            .AddMongoStorage(configuration)
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights();
    }

    static IServiceCollection AddMongoStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ApplicationException("MongoDB connection string is null");
        }

        var builder = new NStorageBuilder(services, configuration);
        builder.AddMongoStorage(connectionString, Assembly.GetExecutingAssembly());
        return services;
    }
}
