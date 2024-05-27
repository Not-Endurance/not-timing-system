using Not.Startup;
using Serilog;

namespace Not.Logging.Filesystem;

public class FilesystemLoggerInitalizer : IInitializer
{
    private readonly IFilesystemLoggerConfiguration _configuration;

    public FilesystemLoggerInitalizer(IFilesystemLoggerConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Run()
    {
        SingleLoggerValidator.Validate();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File($"{_configuration.Directory}/log.txt")
            .CreateLogger();
    }
}
