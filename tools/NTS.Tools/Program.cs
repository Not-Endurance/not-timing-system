using NTS.Tools.Watcher;
using NTS.Tools.NameMigration;

return args.FirstOrDefault() switch
{
    "watcher" => await RunWatcher(),
    "migrate-names" => await RunNameMigration(args.Skip(1).ToArray()),
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
          migrate-names        Migrate Athlete, Horse, Official, and snapshot names to Name/NameEnglish
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

static async Task<int> RunNameMigration(string[] args)
{
    return await NameMigrationTool.Run(args);
}
