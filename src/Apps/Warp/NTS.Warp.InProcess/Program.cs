using Microsoft.Extensions.Logging;
using Not.SystemProcess;
using NTS.Warp;
using NTS.Warp.InProcess;
using static NTS.Warp.InProcess.WarpInProcessConstants;
# if RELEASE
using Not.Filesystem;
using Not.Logging;
using Not.Logging.Builder;
using Serilog;
#endif

var builder = Warp.CreateBuilder(args);

if (args.Any())
{
    var parentPidArgument = args.FirstOrDefault(arg => arg.Contains(PARENT_PID_KEY));
    if (parentPidArgument == null)
    {
        throw new ApplicationException(
            "Parent PID not found in Warp arguments. "
                + "PID is necessary in order to terminate the local Warp instance when Judge closes"
        );
    }
    var parentProcessId = parentPidArgument[PARENT_PID_KEY.Length..];
    builder.Services.AddProcessTether(parentProcessId);
}

builder.Services.RegisterServices(builder.Configuration);

// TODO: configure using Judge appsettings;
builder.Logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Information);
builder.Logging.AddFilter("Microsoft.AspNetCore.Http.Connections", LogLevel.Information);
# if RELEASE
builder.Logging.AddSerilog();
builder
    .Services.AddNLogging()
    .AddFilesystemLogger("NTS.Warp");
#endif

var app = builder.Build();
Warp.Start(app, "11337");
