using Not;
using Not.Injection;
using NTS.Domain.Objects;

namespace NTS.Judge.Blazor.Services;

public class Logger : ILogger
{
    private readonly IFilesystemService fileService;

	public Logger(IFilesystemService fileService)
    {
        this.fileService = fileService;
	}

	public void Log(string type, string message)
	{
		var filename = $"{DateTime.Now.ToString("dd-hh.mm.ss")}_{type}.txt";
        var path = Path.Combine("C:\\tmp\\nts\\logs", filename);
        fileService.Write(path, message);
    }

	public void LogEvent(Snapshot snapshot)
    {
        var filename = "witness-events.log";
        var now = DateTime.Now;
        var message = $"{now}: Event" + Environment.NewLine + 
            $"  Type: {snapshot.Type}" + Environment.NewLine + 
            $"  RFID: {snapshot.Number}" + Environment.NewLine +
            $"  Time: {snapshot.Timestamp}" + Environment.NewLine;
        fileService.Write(this.BuildFilePath(filename), message);
    }

    public void LogEventError(Exception exception)
    {
        var filename = "witness-event-errors.log";
        var now = DateTime.Now;
        var message =
            $"{now}: {exception.Message}" + Environment.NewLine +
            exception.StackTrace + Environment.NewLine;
            fileService.Write(this.BuildFilePath(filename), message);
    }

    private string BuildFilePath(string filename)
    {
        var dir = $"{Directory.GetCurrentDirectory()}/logs";
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return Path.Combine(dir, filename);
    }
}

public interface ILogger : ITransientService
{
    void Log(string type, string message);
    void LogEvent(Snapshot snapshot);
    void LogEventError(Exception exception);
}
