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

var parentPidArgument = args.First(arg => arg.Contains(PARENT_PID_KEY));
if (parentPidArgument.Any())
{
    builder.Services.AddSingleton<ProcessServiceContext>(provider =>
    {
        var parentProcessId = parentPidArgument.Substring(PARENT_PID_KEY.Length);
        return new ProcessServiceContext(parentProcessId);
    });
    builder.Services.AddHostedService<ProcessService>();
}

builder
    .ConfigureLogging()
    .AddFilesystemLogger(logFileConfig =>
    {
        logFileConfig.Path = FileContextHelper.GetAppDirectory();
        logFileConfig.Name = ContextHelper.ConfigureApplicationName("NTS.Judge.Server");
    });

var app = builder.Build();

app.Urls.Add("http://*:11337");

app.MapHub<JudgeRpcHub>(ApplicationConstants.JUDGE_HUB);
app.MapHub<WitnessRpcHub>(ApplicationConstants.WITNESS_HUB);

foreach (var initializer in app.Services.GetServices<IStartupInitializer>())
{
    initializer.RunAtStartup();
}

app.Run();
