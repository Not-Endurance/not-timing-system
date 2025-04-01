using Not.Logging;
using Not.Logging.Builder;
using Serilog;

namespace NTS.Relay;

public static class WebAppBuilderExtensions
{
    public static NLogBuilder ConfigureLogging(this WebApplicationBuilder builder)
    {
        builder.Logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Trace);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Http.Connections", LogLevel.Trace);
        builder.Logging.AddSerilog();
        return builder.Services.AddNLogging();
    }
}
