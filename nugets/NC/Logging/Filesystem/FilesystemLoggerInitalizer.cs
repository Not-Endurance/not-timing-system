using Microsoft.Extensions.DependencyInjection;
using NC.Files;
using NC.Logging.Builder;
using NC.Startup;
using Serilog;

namespace NC.Logging.Filesystem;

public class FilesystemLoggerInitalizer : IStartupInitializer
{
    readonly IFilesystemContext _context;

    public FilesystemLoggerInitalizer([FromKeyedServices(NLogBuilder.KEY)] IFilesystemContext context)
    {
        _context = context;
    }

    public void RunAtStartup()
    {
        LoggingHelper.PreventDuplicateRegistration();

        var filename = _context.Name != null ? _context.Name + ".Log.txt" : "log.txt";
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File($"{_context.AppDirectory}/{filename}")
            .CreateLogger();
    }
}
