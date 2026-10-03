using NTS.Tools.NameMigration;
using NTS.Tools.ParticipationCopies;
using NTS.Tools.Watcher;

return args.FirstOrDefault() switch
{
    "watcher" => await RunWatcher(),
    "migrate-names" => await RunNameMigration(args.Skip(1).ToArray()),
    "migrate-participation-copies" => await RunParticipationCopiesMigration(args.Skip(1).ToArray()),
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
          migrate-participation-copies
                               Turn the Participation copies in Rankings and Handouts into references
                               and drop the derived values (a dry run unless --apply)
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

static async Task<int> RunParticipationCopiesMigration(string[] args)
{
    return await ParticipationCopiesMigrationTool.Run(args);
}
