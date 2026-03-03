using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Not.Startup;
using NTS.Nexus.HTTP;
using NTS.Nexus.HTTP.Telemetry;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();
builder.Services.ConfigureNexusApi(builder.Configuration);

var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("NTS.Nexus.HTTP.Startup");
MaskedEnvironmentVariableLogger.Log(startupLogger);

await app.Services.Startup();

app.Run();
