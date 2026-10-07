using MongoDB.Driver;

namespace NTS.Tools.Tenants;

/// <summary>The command line of <c>migrate-tenants</c>. See the README of the tools for what it does and the order of the cutover.</summary>
public static class TenantsMigrationTool
{
    const string DEFAULT_DATABASE = "nts";

    public static Task<int> Run(string[] args)
    {
        return Run(args, Console.Out, Console.Error);
    }

    /// <param name="now">The instant an Event is told Live or Historic at, which is the clock of the machine unless a test gives one.</param>
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
        var report = await TenantsMigration.Run(
            database,
            new TenantsOptions
            {
                Apply = options.Apply,
                Environment = options.Environment,
                MainOperatorEmail = options.MainOperator,
            },
            now ?? TimeProvider.System.GetUtcNow()
        );
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
                case "--environment" when i + 1 < args.Length:
                    options.Environment = args[++i];
                    break;
                case "--main-operator" when i + 1 < args.Length:
                    options.MainOperator = args[++i];
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
              dotnet run --project tools/NTS.Tools -- migrate-tenants --connection-string <mongo> [--database nts] [--environment <name>] [--main-operator <email>] [--apply]

            Turns the data of before Tenants into the data of Tenants (ADR-0012): marks the database with its environment,
            completes the accounts with the fields of Identity (the address unconfirmed), places each account in the Tenant of
            the country of its profile, makes the Tenants, stamps every document a Tenant owns with the Tenant of Bulgaria,
            gives every Event that is not yet Historic its Tenant Root as Main Operator, turns the Officials and Operators that
            were linked by email into grants or pending invitations, finds the state a person kept again under their account,
            drops the settings and makes the indexes. A dry run reports all of it and changes nothing. --apply needs
            --environment and refuses, writing nothing, while two accounts share an email, Bulgaria is not among the countries,
            or the database is marked as another environment. Running it again changes nothing, and it is run again once a
            Tenant Root has been seeded, which is what gives the Events their Main Operator.

            Options:
              --connection-string <mongo>   MongoDB connection string.
              --database <name>             Database name. Defaults to nts.
              --environment <name>          Production, Staging or Development: what the database is. Written as its marker.
              --main-operator <email>       The account that becomes the Main Operator of the Events that have none, in place of
                                            the Tenant Root of their Tenant.
              --apply                       Persist changes. Omit for a dry run.
            """
        );
    }

    sealed class Options
    {
        public string? ConnectionString { get; set; }
        public string Database { get; set; } = DEFAULT_DATABASE;
        public string? Environment { get; set; }
        public string? MainOperator { get; set; }
        public bool Apply { get; set; }
        public bool ShowHelp { get; set; }
        public bool IsValid { get; set; } = true;
    }
}
