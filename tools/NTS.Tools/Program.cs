using NTS.Tools.Watcher;

return args.FirstOrDefault() switch
{
    "watcher" => await RunWatcher(),
    "-h" => ShowHelp(),
    "--help" => ShowHelp(),
    "help" => ShowHelp(),
    null => ShowHelp(),
    var command => UnknownCommand(command),
};

static int ShowHelp()
{
    Console.WriteLine(
        """
        Usage:
          dotnet run --project tools/NTS.Tools -- <command> [options]

        Commands:
          watcher              Placeholder watcher command
        """
    );

    return 0;
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command '{command}'.");
    return ShowHelp();
}

static async Task<int> RunWatcher()
{
    await WatcherTool.Run();
    return 0;
}
