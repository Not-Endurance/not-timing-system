using Microsoft.Extensions.Hosting;

namespace Not.ProcessUtility;

public class ProcessService : BackgroundService
{
    readonly ProcessServiceContext _context;

    public ProcessService(ProcessServiceContext context)
    {
        _context = context;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!ProcessExists(_context.ParentProcessId))
            {
                Console.WriteLine(
                    $"Parent process {_context.ParentProcessId} has exited. Shutting down child process..."
                );
                Environment.Exit(0);
            }
            await Task.Delay(2000);
        }
    }

    bool ProcessExists(int pid)
    {
        return System.Diagnostics.Process.GetProcessById(pid) != null;
    }
}
