using MongoDB.Driver;

namespace NTS.Tools.ParticipationCopies;

/// <summary>The command line of <c>migrate-participation-copies</c>. See the README of the tools for the cutover.</summary>
public static class ParticipationCopiesMigrationTool
{
    const string DEFAULT_DATABASE = "nts";

    public static Task<int> Run(string[] args)
    {
        return Run(args, Console.Out, Console.Error);
    }

    /// <returns>0 when it ran, 1 when it was asked wrongly or refused to apply.</returns>
    public static async Task<int> Run(string[] args, TextWriter output, TextWriter error)
    {
        var options = Parse(args, error);
        if (options.ShowHelp)
        {
            ShowHelp(output);
            return options.IsValid ? 0 : 1;
        }

        var database = new MongoClient(options.ConnectionString).GetDatabase(options.Database);
        output.WriteLine($"Database: {options.Database}");
        var report = await ParticipationCopiesMigration.Run(database, options.Apply);
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
              dotnet run --project tools/NTS.Tools -- migrate-participation-copies --connection-string <mongo> [--database nts] [--apply]

            Turns the copy of a Participation inside every Ranking entry and Handout into the id of it, keeps the stored
            rank and the not-ranked mark of each entry, and removes the Total and the derived values of each Phase from
            every Participation. A dry run reports the counts, the Participations that are missing and the copies that
            differ from the stored Participation. --apply refuses to run while a Participation is missing, and running it
            again changes nothing.

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
