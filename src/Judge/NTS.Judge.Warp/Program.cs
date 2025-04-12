# if RELEASE
using Not.Filesystem;
using Not.Logging;
using Not.Logging.Builder;
using Serilog;
#endif
using Microsoft.Extensions.DependencyInjection;
using Not.SystemProcess;
using NTS.Warp;
using static NTS.Judge.Warp.JudgeWarpConstants;

var builder = Warp.CreateBuilder(args);

if (args.Any())
{
    var parentPidArgument = args.First(arg => arg.Contains(PARENT_PID_KEY));
    builder.Services.AddSingleton(provider =>
    {
        var parentProcessId = parentPidArgument.Substring(PARENT_PID_KEY.Length);
        return new ProcessContext(parentProcessId);
    });
    builder.Services.AddHostedService<ProcessTetherLoop>();
}

# if RELEASE
builder.Logging.AddSerilog();
builder.Services
    .AddNLogging()
    .AddFilesystemLogger(logFileConfig =>
    {
        logFileConfig.Path = FileContextHelper.GetAppDirectory("logs");
        logFileConfig.Name = FileContextHelper.ConfigureApplicationName("NTS.Warp");
    });
#endif

Warp.StartApp(builder);
