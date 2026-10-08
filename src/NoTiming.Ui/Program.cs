using System.Reflection;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Not.Application.Environments;
using NoTiming.Ui;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// One origin (ADR-0011): the live connection is the page's own, whatever the environment says, as the Api serves the app,
// the resources and the hub, and the session is a cookie that only that origin is sent.
builder.Configuration.AddInMemoryCollection(
    new Dictionary<string, string?> { ["RpcSettings:Host"] = builder.HostEnvironment.BaseAddress.TrimEnd('/') }
);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddNEnvironmentContext(builder.HostEnvironment.Environment);
builder.Services.AddNoTimingUi(
    builder.Configuration,
    builder.HostEnvironment.BaseAddress,
    Assembly.GetExecutingAssembly()
);

Console.WriteLine($"ASPNETCORE_ENVIRONMENT: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}");
Console.WriteLine(
    $"WasmApplicationEnvironmentName: {Environment.GetEnvironmentVariable("WasmApplicationEnvironmentName")}"
);

var host = builder.Build();
await host.RunAsync();
