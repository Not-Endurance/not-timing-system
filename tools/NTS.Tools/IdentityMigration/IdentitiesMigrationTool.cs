using MongoDB.Driver;

namespace NTS.Tools.IdentityMigration;

/// <summary>
/// The command line of <c>migrate-identities</c> (#636): a dry run that writes a Markdown report and a decisions file and changes
/// nothing in the database. See the README of the tools for the cutover.
/// </summary>
public static class IdentitiesMigrationTool
{
    const string DEFAULT_DATABASE = "nts";
    public const string DEFAULT_REPORT = ".tmp/migrate-identities/report.md";
    public const string DEFAULT_DECISIONS = ".tmp/migrate-identities/decisions.json";

    public static Task<int> Run(string[] args)
    {
        return Run(args, Console.Out, Console.Error);
    }

    /// <returns>0 when it ran, 1 when it was asked wrongly or the decisions file could not be read.</returns>
    public static async Task<int> Run(string[] args, TextWriter output, TextWriter error)
    {
        var options = Parse(args, error);
        if (options.ShowHelp)
        {
            ShowHelp(output);
            return options.IsValid ? 0 : 1;
        }

        var read = File.Exists(options.Decisions) ? await File.ReadAllTextAsync(options.Decisions) : null;
        IdentityDecisions decisions;
        try
        {
            decisions = IdentityDecisions.Parse(read ?? "");
        }
        catch (FormatException exception)
        {
            error.WriteLine($"{options.Decisions}: {exception.Message}");
            error.WriteLine("Nothing was written.");
            return 1;
        }

        var database = new MongoClient(options.ConnectionString).GetDatabase(options.Database);
        output.WriteLine($"Database: {options.Database}");
        output.WriteLine("Dry-run identity migration. Nothing is written to the database.");
        var plan = await IdentitiesMigration.Plan(database, decisions);

        await OutputFiles.Write(options.Report, IdentityReport.Render(plan, options.Database));
        var toDecide = plan.Kinds.SelectMany(x => x.Entries).Where(x => !x.IsDecided).Select(x => x.Key);
        var saved = await OutputFiles.SaveDecisions(options.Decisions, read, decisions.ToJson(toDecide));

        foreach (var kind in plan.Kinds)
        {
            output.WriteLine(
                $"{kind.Kind}s: {kind.Copies.Count} copies, {kind.Identities.Count} identities "
                    + $"({kind.RowsToCreate} rows to create, {kind.DuplicateRows} Setup duplicates to merge), "
                    + $"{kind.ToReview} to review"
            );
        }

        output.WriteLine(
            $"Decisions that match nothing: {plan.UnusedDecisions.Count}; "
                + $"that contradict each other: {plan.Kinds.Sum(x => x.Contradictions.Count)}"
        );
        output.WriteLine($"Report: {options.Report}");
        output.WriteLine($"Decisions: {options.Decisions} ({Describe(saved)})");
        return 0;
    }

    static string Describe(DecisionsSaved saved)
    {
        return saved switch
        {
            DecisionsSaved.Written => "written",
            DecisionsSaved.Unchanged => "unchanged",
            _ =>
                "NOT written: it was changed while the dry run read the database, so it is as you saved it; run the dry run again",
        };
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
                case "--report" when i + 1 < args.Length:
                    options.Report = args[++i];
                    break;
                case "--decisions" when i + 1 < args.Length:
                    options.Decisions = args[++i];
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

        if (!options.ShowHelp && string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            options.ShowHelp = true;
            options.IsValid = false;
            error.WriteLine("--connection-string is required.");
        }

        if (
            !options.ShowHelp
            && string.Equals(
                Path.GetFullPath(options.Report),
                Path.GetFullPath(options.Decisions),
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            options.ShowHelp = true;
            options.IsValid = false;
            error.WriteLine("--report and --decisions must be two files.");
        }

        return options;
    }

    static void ShowHelp(TextWriter output)
    {
        output.WriteLine(
            $$"""
            Usage:
              dotnet run --project tools/NTS.Tools -- migrate-identities --connection-string <mongo> [--database nts] [--report <path>] [--decisions <path>]

            A dry run: it reads the Athletes and the Horses wherever they are stored, groups the copies that are one Athlete or
            one Horse by the rules of the registry (ADR-0010), and writes a report and a file of decisions. It changes nothing in
            the database. The report counts the copies and the identities, lists every automatic merge by the rule that made it,
            and lists what is left to decide with the evidence for each item. The decisions file is a template with every item
            still to decide: set an item to "same" or "different", or set a rule for a whole class under "rules", and run the dry
            run again; an item that is decided is no longer listed. Both files hold real names and are never committed.

            Options:
              --connection-string <mongo>   MongoDB connection string.
              --database <name>             Database name. Defaults to nts.
              --report <path>               Where the Markdown report is written. Defaults to {{DEFAULT_REPORT}}.
              --decisions <path>            The decisions file, read when it exists and written back with the items to decide.
                                            Defaults to {{DEFAULT_DECISIONS}}.
            """
        );
    }

    sealed class Options
    {
        public string? ConnectionString { get; set; }
        public string Database { get; set; } = DEFAULT_DATABASE;
        public string Report { get; set; } = DEFAULT_REPORT;
        public string Decisions { get; set; } = DEFAULT_DECISIONS;
        public bool ShowHelp { get; set; }
        public bool IsValid { get; set; } = true;
    }
}
