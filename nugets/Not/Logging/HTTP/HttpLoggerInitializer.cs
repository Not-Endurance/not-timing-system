using Not.Startup;
using Serilog;

namespace Not.Logging.HTTP;

public class HttpLoggerInitializer : IStartupInitializer
{
    readonly IHttpLoggerConfiguration _configuration;

    public HttpLoggerInitializer(IHttpLoggerConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void RunAtStartup()
    {
        LoggingHelper.Validate();

        // TODO: this has to be reworked to accept a hostResolutionTask and await it in the background
        // otherwise Host will always be null
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Http(_configuration.Host!, queueLimitBytes: null)
            .CreateLogger();
    }
}
