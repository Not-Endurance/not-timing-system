using Microsoft.Extensions.DependencyInjection;
using Not.Filesystem;
using Not.Logging.Builder;
using Not.Startup;
using Serilog;

namespace Not.Logging.Filesystem;

public class FilesystemLoggerInitalizer : IStartupInitializer
{
    readonly IFileContext _context;

    public FilesystemLoggerInitalizer([FromKeyedServices(NLogBuilder.KEY)] IFileContext context)
    {
        _context = context;
    }

    public void RunAtStartup()
    {
        LoggingHelper.Validate();

        var filename = _context.Name != null ? _context.Name + ".Log.txt" : "log.txt";
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File($"{_context.Path}/{filename}")
            .CreateLogger();
    }
}
