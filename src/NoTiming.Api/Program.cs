using Not.Application.Configurations;
using Not.Application.Environments;
using NoTiming.Api.Hosting;

var builder = WebApplication.CreateBuilder(args);
if (EnvironmentHelper.IsLocalhost())
{
    builder.Configuration.AddSecrets($"nts-{builder.Environment.EnvironmentName}");
}

builder.Services.AddNoTimingApi(builder.Configuration, builder.Environment);

var app = builder.Build();
app.UseNoTimingApi();
app.Run();

public partial class Program;
