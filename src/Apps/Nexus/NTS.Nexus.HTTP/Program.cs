using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Not.Startup;
using NTS.Nexus.HTTP;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();
builder.Services.ConfigureNexusApi(builder.Configuration);

var app = builder.Build();
await app.Services.Startup();

app.Run();
