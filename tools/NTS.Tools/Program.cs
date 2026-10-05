using NTS.Tools.Developer;
using NTS.Tools.NameMigration;
using NTS.Tools.ParticipationCopies;
using NTS.Tools.PhaseTimes;
using NTS.Tools.Watcher;

return args.FirstOrDefault() switch
{
    "watcher" => await RunWatcher(),
    "migrate-names" => await RunNameMigration(args.Skip(1).ToArray()),
    "migrate-participation-copies" => await RunParticipationCopiesMigration(args.Skip(1).ToArray()),
    "migrate-phase-times" => await RunPhaseTimesMigration(args.Skip(1).ToArray()),
    "seed-tenant-root" => await DeveloperTool.SeedTenantRoot(args.Skip(1).ToArray()),
    "grant-developer" => await DeveloperTool.GrantDeveloper(args.Skip(1).ToArray()),
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
          migrate-phase-times  Turn the flat Arrive, Present and Represent times of every Phase into time events
                               and drop the snapshot-results collection (a dry run unless --apply)
          seed-tenant-root     Make an account a Tenant Root of a Tenant, which makes the Tenant operational
                               (a dry run unless --apply)
          grant-developer      Make an account the Developer (a dry run unless --apply)
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

static async Task<int> RunPhaseTimesMigration(string[] args)
{
    return await PhaseTimesMigrationTool.Run(args);
}
