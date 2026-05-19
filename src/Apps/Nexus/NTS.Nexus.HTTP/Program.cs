using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Not.Application.Configurations;
using Not.Localization;
using Not.Startup;
using NTS.Nexus.HTTP;
using NTS.Nexus.HTTP.Cors;
using NTS.Nexus.HTTP.Telemetry;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Configuration.AddNAppsettings(typeof(NtsNexusApiServices).Assembly, "nts");
builder.Configuration.AddEnvironmentVariables();

builder.ConfigureFunctionsWebApplication();
builder.UseMiddleware<FunctionsCorsMiddleware>();
builder.UseMiddleware<ErrorHandlerMiddleware>();
builder.Services.ConfigureNexusApi(builder.Configuration);

var app = builder.Build();

LocalizationHelper.Configure(app.Services.GetRequiredService<IStringLocalizer>());

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("NTS.Nexus.HTTP.Startup");
MaskedEnvironmentVariableLogger.Log(startupLogger);

await app.Services.Startup();

app.Run();
