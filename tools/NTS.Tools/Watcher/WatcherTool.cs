namespace NTS.Tools.Watcher;

public static class WatcherTool
{
    public static Task Run()
    {
        Console.WriteLine("Watcher tool is not configured for the current project layout.");
        return Task.CompletedTask;
    }
}
