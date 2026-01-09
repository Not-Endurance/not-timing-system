using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Injection;
using NTS.Storage;

namespace NTS.Nexus.HTTP;

internal static class NtsNexusApiServices
{
    public static IServiceCollection ConfigureNexusApi(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
        services.ConfigureNtsStorage(configuration).AddMongoStorage(connectionString);
        return services
            .AddNConventionalServices(Assembly.GetExecutingAssembly())
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights();
    }
}
