using MongoDB.Driver;
using Not.Storage.Mongo;

namespace NTS.Tools.EnvironmentMarking;

/// <summary>
/// <c>mark-environment</c> (#607): writes the environment marker of a database, which says whether it is production, staging
/// or a developer's own. A dry run says what it would write. A marker is never changed to another name, so marking a
/// production database as such is the first thing to do with it, and marking one that says otherwise is refused. The marker
/// is also written by <c>migrate-tenants --apply</c> and by <c>seed-staging --apply</c> for the databases they prepare.
/// </summary>
public static class MarkEnvironmentTool
{
    const string DEFAULT_DATABASE = "nts";

    public static Task<int> Run(string[] args)
    {
        return Run(args, Console.Out, Console.Error);
    }

    /// <returns>0 when it ran, 1 when it was asked wrongly or refused.</returns>
    public static async Task<int> Run(string[] args, TextWriter output, TextWriter error, DateTimeOffset? now = null)
    {
        var options = Parse(args, error);
        if (options.ShowHelp)
        {
            ShowHelp(output);
            return options.IsValid ? 0 : 1;
        }

        var name = EnvironmentMarker.Canonical(options.Environment);
        if (name == null)
        {
            error.WriteLine(
                $"'{options.Environment}' is not an environment: use {string.Join(", ", EnvironmentMarker.Names)}."
            );
            return 1;
        }

        var database = new MongoClient(options.ConnectionString).GetDatabase(options.Database);
        var existing = await EnvironmentMarker.ReadAsync(database);
        if (existing != null)
        {
            if (string.Equals(existing, name, StringComparison.Ordinal))
            {
                output.WriteLine($"The database is marked {existing} already: nothing to do.");
                return 0;
            }

            error.WriteLine(
                $"The database is marked {existing}, and a marker is not changed to another name: remove the document of the environment collection by hand if it is wrong."
            );
            return 1;
        }

        if (!options.Apply)
        {
            output.WriteLine($"Dry run. Would mark the database as {name}.");
            output.WriteLine("Nothing was changed. Run again with --apply to persist.");
            return 0;
        }

        await EnvironmentMarker.WriteAsync(database, name, now ?? TimeProvider.System.GetUtcNow());
        output.WriteLine($"The database is marked {name}.");
        return 0;
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
                case "--environment" when i + 1 < args.Length:
                    options.Environment = args[++i];
                    break;
                case "--apply":
                    options.Apply = true;
                    break;
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    break;
                default:
                    options.Fail(error, $"Unknown or incomplete option '{args[i]}'.");
                    break;
            }
        }

        if (!options.ShowHelp && string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            options.Fail(error, "--connection-string is required.");
        }

        if (!options.ShowHelp && string.IsNullOrWhiteSpace(options.Environment))
        {
            options.Fail(error, "--environment is required.");
        }

        return options;
    }

    static void ShowHelp(TextWriter output)
    {
        output.WriteLine(
            """
            Usage:
              dotnet run --project tools/NTS.Tools -- mark-environment --connection-string <mongo> --environment <name> [--database nts] [--apply]

            Writes the marker that says which environment a database is: Production, Staging or Development. The commands that
            must never touch a production database, and the local sign-in as somebody, look at it first. A marker is not changed
            to another name. A dry run says what would be written.

            Options:
              --connection-string <mongo>   MongoDB connection string.
              --environment <name>          Production, Staging or Development.
              --database <name>             Database name. Defaults to nts.
              --apply                       Persist the marker. Omit for a dry run.
            """
        );
    }

    sealed class Options
    {
        public string? ConnectionString { get; set; }
        public string Database { get; set; } = DEFAULT_DATABASE;
        public string? Environment { get; set; }
        public bool Apply { get; set; }
        public bool ShowHelp { get; set; }
        public bool IsValid { get; set; } = true;

        public void Fail(TextWriter error, string message)
        {
            ShowHelp = true;
            IsValid = false;
            error.WriteLine(message);
        }
    }
}
