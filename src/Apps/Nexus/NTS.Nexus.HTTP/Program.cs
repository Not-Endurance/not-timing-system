using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Not.Application.Configurations;
using Not.Startup;
using NTS.Nexus.HTTP;
using NTS.Nexus.HTTP.Cors;
using NTS.Nexus.HTTP.Telemetry;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Configuration.AddNAppsettings(typeof(NtsNexusApiServices).Assembly, "nts");
builder.Configuration.AddEnvironmentVariables();

builder.ConfigureFunctionsWebApplication();
builder.UseMiddleware<FunctionsCorsMiddleware>();
builder.UseMiddleware<FunctionInvocationTelemetryMiddleware>();
builder.Services.ConfigureNexusApi(builder.Configuration);

var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("NTS.Nexus.HTTP.Startup");
MaskedEnvironmentVariableLogger.Log(startupLogger);

await app.Services.Startup();

app.Run();
