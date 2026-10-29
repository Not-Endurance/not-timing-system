using MongoDB.Driver;

namespace NTS.Tools.EventLiveness;

/// <summary>The command line of <c>migrate-event-liveness</c>. See the README of the tools for the cutover.</summary>
public static class EventLivenessMigrationTool
{
    const string DEFAULT_DATABASE = "nts";

    public static Task<int> Run(string[] args)
    {
        return Run(args, Console.Out, Console.Error);
    }

    /// <param name="now">The instant the rule is applied at, which is the clock of the machine unless a test gives one.</param>
    /// <returns>0 when it ran, 1 when it was asked wrongly or refused to apply.</returns>
    public static async Task<int> Run(string[] args, TextWriter output, TextWriter error, DateTimeOffset? now = null)
    {
        var options = Parse(args, error);
        if (options.ShowHelp)
        {
            ShowHelp(output);
            return options.IsValid ? 0 : 1;
        }

        var database = new MongoClient(options.ConnectionString).GetDatabase(options.Database);
        output.WriteLine($"Database: {options.Database}");
        var report = await EventLivenessMigration.Run(database, options.Apply, now ?? TimeProvider.System.GetUtcNow());
        report.WriteTo(output);
        return report.Refused ? 1 : 0;
    }

    static Options Parse(string[] args, TextWriter error)
    {
        var options = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--connection-string" when i + 1 < args.Length:
                    options.ConnectionString = args[++i];
                    break;
                case "--database" when i + 1 < args.Length:
                    options.Database = args[++i];
                    break;
                case "--apply":
                    options.Apply = true;
                    break;
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    break;
                default:
                    options.ShowHelp = true;
                    options.IsValid = false;
                    error.WriteLine($"Unknown or incomplete option '{args[i]}'.");
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionString) && !options.ShowHelp)
        {
            options.ShowHelp = true;
            options.IsValid = false;
            error.WriteLine("--connection-string is required.");
        }

        return options;
    }

    static void ShowHelp(TextWriter output)
    {
        output.WriteLine(
            """
            Usage:
              dotnet run --project tools/NTS.Tools -- migrate-event-liveness --connection-string <mongo> [--database nts] [--apply]

            An Event is Live until the end of its last day and Historic from then on (ADR-0007), and no flag is stored: this
            removes the IsActive flag from every Event. A dry run counts the Events that are Live and Historic under the rule
            and lists the Events that were inactive and whose last day is still ahead, because under the rule they are Live
            again. --apply refuses to run while that list is not empty, or while an EndDay is not a date, and running it again
            changes nothing.

            Options:
              --connection-string <mongo>   MongoDB connection string.
              --database <name>             Database name. Defaults to nts.
              --apply                       Persist changes. Omit for a dry run.
            """
        );
    }

    sealed class Options
    {
        public string? ConnectionString { get; set; }
        public string Database { get; set; } = DEFAULT_DATABASE;
        public bool Apply { get; set; }
        public bool ShowHelp { get; set; }
        public bool IsValid { get; set; } = true;
    }
}
