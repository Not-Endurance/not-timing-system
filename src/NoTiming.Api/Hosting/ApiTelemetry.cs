using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace NoTiming.Api.Hosting;

internal static class ApiTelemetry
{
    public const string SERVICE_NAME = "NoTiming.Api";

    /// <summary>
    /// Traces and metrics for requests, outgoing calls and the live hub. Azure Monitor receives them when the App
    /// Service has an Application Insights connection string, and an OTLP collector (for example the Aspire dashboard
    /// of docker/nexus.yaml) when OTEL_EXPORTER_OTLP_ENDPOINT is set. With neither, nothing is exported.
    /// </summary>
    public static IServiceCollection AddApiTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var telemetry = services
            .AddOpenTelemetry()
            .ConfigureResource(resource =>
                resource.AddService(
                    SERVICE_NAME,
                    serviceVersion: typeof(ApiTelemetry).Assembly.GetName().Version?.ToString()
                )
            )
            .WithTracing(tracing =>
                tracing
                    .AddSource("Microsoft.AspNetCore.SignalR.Server")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
            )
            .WithMetrics(metrics =>
                metrics
                    .AddMeter("Microsoft.AspNetCore.Http.Connections")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
            );

        if (!string.IsNullOrWhiteSpace(configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            telemetry.UseAzureMonitor();
        }

        if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.UseOtlpExporter();
        }

        return services;
    }
}
