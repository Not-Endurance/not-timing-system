using System.Diagnostics;
using Microsoft.Extensions.Hosting;

namespace Not.SystemProcess;

public class ProcessTetherLoop : BackgroundService
{
    readonly ProcessTetherContext _tetherContext;
    readonly TimeSpan _interval = TimeSpan.FromSeconds(2);

    public ProcessTetherLoop(ProcessTetherContext tetherContext)
    {
        _tetherContext = tetherContext;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            if (!ProcessExists(_tetherContext.ParentProcessId))
            {
                Console.WriteLine(
                    $"Parent process {_tetherContext.ParentProcessId} has exited. Shutting down child process..."
                );
                Environment.Exit(0);
            }
            await Task.Delay(_interval, cancellation);
        }
    }

    bool ProcessExists(int pid)
    {
        try
        {
            Process.GetProcessById(pid);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
