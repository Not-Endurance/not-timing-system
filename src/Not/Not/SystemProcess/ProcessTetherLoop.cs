using System.Diagnostics;
using Microsoft.Extensions.Hosting;

namespace Not.SystemProcess;

public class ProcessTetherLoop : BackgroundService
{
    readonly ProcessContext _context;

    public ProcessTetherLoop(ProcessContext context)
    {
        _context = context;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            if (!ProcessExists(_context.ParentProcessId))
            {
                Console.WriteLine(
                    $"Parent process {_context.ParentProcessId} has exited. Shutting down child process..."
                );
                Environment.Exit(0);
            }
            await Task.Delay(2000, cancellation);
        }
    }

    bool ProcessExists(int pid)
    {
        return Process.GetProcessById(pid) != null;
    }
}
