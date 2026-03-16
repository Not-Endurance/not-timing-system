using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Not.Application.Configurations;
using Not.Startup;
using NTS.Witness;
using NTS.Witness.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

await AddLocalhostOverrideSettings(builder);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddNtsWitnessWeb(builder.Configuration);
builder.Services.AddNtsWitness(builder.Configuration, builder.HostEnvironment.BaseAddress);

Console.WriteLine($"ASPNETCORE_ENVIRONMENT: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}");
Console.WriteLine(
    $"WasmApplicationEnvironmentName: {Environment.GetEnvironmentVariable("WasmApplicationEnvironmentName")}"
);

var host = builder.Build();
await host.Services.Startup();
await host.RunAsync();

static Task AddLocalhostOverrideSettings(WebAssemblyHostBuilder builder)
{
    if (!IsLocalhost(builder))
    {
        return Task.CompletedTask;
    }

    var environment = builder.HostEnvironment.Environment;
    if (environment != "Staging" && environment != "Production")
    {
        return Task.CompletedTask;
    }

    var fileName = $"localhostsettings.{environment}.json";
    builder.Configuration.AddEmbeddedJsonFile(fileName, optional: true, typeof(App).Assembly);
    return Task.CompletedTask;
}

static bool IsLocalhost(WebAssemblyHostBuilder builder)
{
    if (!Uri.TryCreate(builder.HostEnvironment.BaseAddress, UriKind.Absolute, out var uri))
    {
        return false;
    }

    return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
}
