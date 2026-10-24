using MongoDB.Driver;
using NTS.Domain.Setup.Services.Identities;

namespace NTS.Tools.IdentityMigration;

/// <summary>
/// The command line of <c>migrate-identities</c> (#636, #637): a dry run that writes a Markdown report and a decisions file and
/// changes nothing in the database, and with <c>--apply</c> the conversion of every id, the merges, the final placings and the
/// verification. See the README of the tools for the cutover.
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

    public static Task<int> Run(string[] args, TextWriter output, TextWriter error)
    {
        return Run(args, output, error, DateTimeOffset.UtcNow);
    }

    /// <param name="now">The instant an Event is told Live or Historic at.</param>
    /// <returns>
    /// 0 when it ran, and for an apply when it was not refused and every check holds; 1 when it was asked wrongly, the decisions file
    /// could not be read, an apply was refused or stopped, or a check of the verification failed.
    /// </returns>
    public static async Task<int> Run(string[] args, TextWriter output, TextWriter error, DateTimeOffset now)
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

        IMongoClient client;
        try
        {
            client = new MongoClient(options.ConnectionString);
        }
        catch (Exception)
        {
            // the driver refuses a string it cannot use in one of several ways, and what it says may quote the string, which is never printed
            error.WriteLine("--connection-string is not a valid MongoDB connection string.");
            return 1;
        }

        IMongoDatabase database;
        try
        {
            database = client.GetDatabase(options.Database);
        }
        catch (ArgumentException exception)
        {
            error.WriteLine($"--database: {exception.Message}");
            return 1;
        }

        output.WriteLine($"Database: {options.Database}");
        if (options.Apply)
        {
            return await Apply(database, options, decisions, read, output, error, now);
        }

        output.WriteLine("Dry-run identity migration. Nothing is written to the database.");
        if (await new IdentityPlanStore(database, null).ExistsAsync())
        {
            output.WriteLine(
                "A run of --apply that stopped is kept in this database: --apply finishes it. What follows is of data that is "
                    + "partly converted, and means little."
            );
        }

        var inputs = await IdentitiesMigration.Read(database);
        var plan = inputs.Plan(decisions);
        var cutover = await IdentitiesMigration.Preview(database, inputs, plan, now);

        await OutputFiles.Write(options.Report, IdentityReport.Render(plan, options.Database, cutover));
        var saved = await SaveDecisions(options, plan, decisions, read);

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
        PrintCutover(output, cutover);
        output.WriteLine($"Report: {options.Report}");
        output.WriteLine($"Decisions: {options.Decisions} ({Describe(saved)})");
        return 0;
    }

    static async Task<int> Apply(
        IMongoDatabase database,
        Options options,
        IdentityDecisions decisions,
        string? read,
        TextWriter output,
        TextWriter error,
        DateTimeOffset now
    )
    {
        output.WriteLine(
            "Applying the identity migration. Each step writes only what is not written yet: a run that stops is finished by running it again."
        );
        var saved = DecisionsSaved.Unchanged;
        ApplyResult result;
        try
        {
            result = await IdentitiesMigration.Apply(
                database,
                decisions,
                new ApplyOptions
                {
                    Now = now,
                    AfterStep = step =>
                    {
                        output.WriteLine($"  done: {step}");
                        return Task.CompletedTask;
                    },
                    PlanMade = async plan =>
                    {
                        await OutputFiles.Write(options.Report, IdentityReport.Render(plan, options.Database));
                        saved = await SaveDecisions(options, plan, decisions, read);
                    },
                }
            );
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            error.WriteLine($"The run stopped: {exception.Message}");
            error.WriteLine("What was written stays written. Run it again to finish.");
            return 1;
        }

        if (result.Refusals.Count > 0)
        {
            output.WriteLine(
                result.Steps.Count == 0
                    ? "Refused. Nothing was written to the database:"
                    : $"Refused after {result.Steps[^1]}. What was written stays, and the run is kept in the database: resolve this and run it again, or restore the backup and start again if it takes a change of a name, an id or a decision:"
            );
            foreach (var refusal in result.Refusals)
            {
                output.WriteLine($"  - {refusal}");
            }

            return 1;
        }

        output.WriteLine(result.Resumed ? "Finished a run that had stopped." : "Applied.");
        output.WriteLine(
            $"Setup rows made: {result.RowsMade}; merged away: {result.RowsMerged}. Rankings finalised: {result.Rankings.Finalised.Count}."
        );
        output.WriteLine("Verification:");
        PrintChecks(output, result.Checks);
        output.WriteLine($"Report: {options.Report}");
        output.WriteLine($"Decisions: {options.Decisions} ({Describe(saved)})");
        return result.Succeeded ? 0 : 1;
    }

    static Task<DecisionsSaved> SaveDecisions(
        Options options,
        IdentityPlan plan,
        IdentityDecisions decisions,
        string? read
    )
    {
        var toDecide = plan.Kinds.SelectMany(x => x.Entries).Where(x => !x.IsDecided).Select(x => x.Key);
        return OutputFiles.SaveDecisions(options.Decisions, read, decisions.ToJson(toDecide));
    }

    static void PrintCutover(TextWriter output, PlannedCutover cutover)
    {
        output.WriteLine(
            $"Rankings of Events that have ended with no placings, finalised by an apply: {cutover.Rankings.Count(x => x.State == PlacingsState.Unplaced)}"
        );
        output.WriteLine(
            $"Rankings that hold only some of their ranks, left alone: {cutover.Rankings.Count(x => x.State == PlacingsState.Partial)}"
        );
        output.WriteLine("Verification of the planned result:");
        PrintChecks(output, cutover.Checks);
        output.WriteLine($"An apply would be refused: {cutover.Refusals.Count}");
        foreach (var refusal in cutover.Refusals)
        {
            output.WriteLine($"  - {refusal}");
        }
    }

    static void PrintChecks(TextWriter output, IReadOnlyList<VerificationCheck> checks)
    {
        foreach (var check in checks)
        {
            output.WriteLine($"  {(check.Passed ? "PASS" : "FAIL")} {check.Name}");
            foreach (var detail in check.Details)
            {
                output.WriteLine($"    - {detail}");
            }
        }
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
              dotnet run --project tools/NTS.Tools -- migrate-identities --connection-string <mongo> [--database nts] [--report <path>] [--decisions <path>] [--apply]

            A dry run unless --apply is given: it reads the Athletes and the Horses wherever they are stored, groups the copies that
            are one Athlete or one Horse by the rules of the registry (ADR-0010), and writes a report and a file of decisions. It
            changes nothing in the database. The report counts the copies and the identities, lists every automatic merge by the
            rule that made it, and lists what is left to decide with the evidence for each item. The decisions file is a template
            with every item still to decide: set an item to "same" or "different", or set a rule for a whole class under "rules", and
            run the dry run again; an item that is decided is no longer listed. Both files hold real names and are never committed.
            The dry run also says what an apply would finalise, verifies the result an apply would give, and tells what would
            stop it.

            --apply converts every id and every reference of every collection to a Guid (ADR-0009), merges the Athletes and the
            Horses that are the same and gives each a row of Setup (ADR-0010), stores the final placings of the Rankings of the
            Events that have ended, and verifies the result: it prints pass or fail for each check and exits with 1 if one fails.
            It is refused, and writes nothing, for what the data and the decisions do not agree on. Each step writes only what is
            not written yet, so a run that stopped is finished by running it again. Run it with the hosts stopped, after the
            other migrations, on a restored copy first.

            Options:
              --connection-string <mongo>   MongoDB connection string.
              --database <name>             Database name. Defaults to nts.
              --report <path>               Where the Markdown report is written. Defaults to {{DEFAULT_REPORT}}.
              --decisions <path>            The decisions file, read when it exists and written back with the items to decide.
                                            Defaults to {{DEFAULT_DECISIONS}}.
              --apply                       Convert, merge, finalise and verify; without it nothing is written to the database.
            """
        );
    }

    sealed class Options
    {
        public string? ConnectionString { get; set; }
        public string Database { get; set; } = DEFAULT_DATABASE;
        public string Report { get; set; } = DEFAULT_REPORT;
        public string Decisions { get; set; } = DEFAULT_DECISIONS;
        public bool Apply { get; set; }
        public bool ShowHelp { get; set; }
        public bool IsValid { get; set; } = true;
    }
}
