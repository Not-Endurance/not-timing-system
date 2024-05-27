using Not.Logging.Filesystem;

namespace NTS.Judge.Startup;

public class JudgeContext : IFilesystemLoggerConfiguration
{
    public string LogDirectory
#if DEBUG
        => "C:\\tmp\\nts-logs";
#else
        => Path.Combine(Directory.GetCurrentDirectory(), "logs");
#endif

    string IFilesystemLoggerConfiguration.Directory => LogDirectory;
}
