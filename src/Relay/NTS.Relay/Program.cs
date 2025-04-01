using Not.Filesystem;
using Not.Logging.Builder;
using Not.ProcessUtility;
using Not.Startup;
using NTS.Application;
using NTS.Relay;
using NTS.Relay.RPC;
using static NTS.Relay.Constants;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHub();

if (args.Any())
{
    var parentPidArgument = args.First(arg => arg.Contains(PARENT_PID_KEY));
    builder.Services.AddSingleton(provider =>
    {
        var parentProcessId = parentPidArgument.Substring(PARENT_PID_KEY.Length);
        return new ProcessServiceContext(parentProcessId);
    });
    builder.Services.AddHostedService<ProcessService>();
}

#if DEBUG
builder.Logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Trace);
builder.Logging.AddFilter("Microsoft.AspNetCore.Http.Connections", LogLevel.Trace);
#else
builder
    .ConfigureLogging()
    .AddFilesystemLogger(logFileConfig =>
    {
        logFileConfig.Path = FileContextHelper.GetAppDirectory("logs");
        logFileConfig.Name = FileContextHelper.ConfigureApplicationName("NTS.Relay");
    });
#endif

var app = builder.Build();

app.Urls.Add("http://*:11337");

app.MapHub<JudgeRpcHub>(ApplicationConstants.JUDGE_HUB);
app.MapHub<WitnessRpcHub>(ApplicationConstants.WITNESS_HUB);

foreach (var initializer in app.Services.GetServices<IStartupInitializer>())
{
    initializer.RunAtStartup();
}

app.Run();
