using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Not.Startup;
using NTS.Witness;
using NTS.Witness.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddNtsWitness(builder.Configuration);

var host = builder.Build();
await host.Services.Startup();
await host.RunAsync();
