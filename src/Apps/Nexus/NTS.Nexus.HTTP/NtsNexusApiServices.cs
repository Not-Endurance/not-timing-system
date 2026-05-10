using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Injection;
using Not.Storage;
using NTS.Application.Cors;
using NTS.Nexus.HTTP.Mongo.Repositories;
using NTS.Nexus.HTTP.Telemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace NTS.Nexus.HTTP;

internal static class NtsNexusApiServices
{
    public static IServiceCollection ConfigureNexusApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddNtsCorsOriginValidation(configuration);
        services.AddTransient<IUserRepository, UserMongoRepository>();

        return services
            .AddNConventionalServices(Assembly.GetExecutingAssembly())
            .AddMongoStorage(configuration)
            .AddNexusTelemetry()
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights();
    }

    static IServiceCollection AddMongoStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MongoDB");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ApplicationException("MongoDB connection string is null");
        }

        var builder = new NStorageBuilder(services, configuration);
        builder.AddMongoStorage(connectionString, Assembly.GetExecutingAssembly());
        return services;
    }

    static IServiceCollection AddNexusTelemetry(this IServiceCollection services)
    {
        services.AddSingleton<ITelemetryService, TelemetryService>();

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource =>
                resource.AddService(
                    NexusTelemetryConstants.SERVICE_NAME,
                    serviceVersion: Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                )
            )
            .WithTracing(builder =>
            {
                builder
                    .SetSampler(new AlwaysOnSampler())
                    .AddSource(NexusTelemetryConstants.ACTIVITY_SOURCE_NAME)
                    .AddSource(NexusTelemetryConstants.STORAGE_ACTIVITY_SOURCE_NAME)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    builder.AddOtlpExporter();
                }

                var enableConsoleExporter =
                    bool.TryParse(Environment.GetEnvironmentVariable("OTEL_ENABLE_CONSOLE_EXPORTER"), out var enabled)
                    && enabled;
                if (enableConsoleExporter)
                {
                    builder.AddConsoleExporter();
                }
            });

        return services;
    }
}
