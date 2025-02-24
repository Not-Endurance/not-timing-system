using Not.Filesystem;
using Not.Logging.Builder;
using Not.ProcessUtility;
using Not.Startup;
using NTS.Application;
using NTS.Relay;
using NTS.Relay.ACL;
using NTS.Relay.RPC;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHub();

if (args.Length > 0)
{
    builder.Services.AddSingleton<ProcessServiceContext>(provider =>
    {
        var parentProcessId = args[0];
        return new ProcessServiceContext(parentProcessId);
    });
    builder.Services.AddHostedService<ProcessService>();
}

builder.ConfigureLogging().AddFilesystemLogger(logFileConfig =>
{
    logFileConfig.Path = FileContextHelper.GetAppDirectory();
    logFileConfig.Name = ContextHelper.ConfigureApplicationName("NTS.Judge.Server");

});

var app = builder.Build();

app.Urls.Add("http://*:11337");

app.MapHub<JudgeRpcHub>(ApplicationConstants.JUDGE_HUB);
app.MapHub<WitnessRpcHub>(Constants.RPC_ENDPOINT); // TODO: change to NtsApplicationConstants.WITNESS_HUB

foreach (var initializer in app.Services.GetServices<IStartupInitializer>())
{
    initializer.RunAtStartup();
}

app.Run();
