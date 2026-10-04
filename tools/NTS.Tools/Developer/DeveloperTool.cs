using MongoDB.Driver;

namespace NTS.Tools.Developer;

/// <summary>
/// The command lines of <c>seed-tenant-root</c> and <c>grant-developer</c> (ADR-0012). Like the migrations, they are a
/// dry run unless they are given <c>--apply</c>, and they print neither the connection string nor anything the account
/// keeps. See the README of the tools.
/// </summary>
public static class DeveloperTool
{
    const string DEFAULT_DATABASE = "nts";

    public static Task<int> SeedTenantRoot(string[] args)
    {
        return SeedTenantRoot(args, Console.Out, Console.Error);
    }

    public static Task<int> GrantDeveloper(string[] args)
    {
        return GrantDeveloper(args, Console.Out, Console.Error);
    }

    /// <returns>0 when it ran, 1 when it was asked wrongly or refused.</returns>
    public static async Task<int> SeedTenantRoot(string[] args, TextWriter output, TextWriter error)
    {
        var options = Parse(args, error, needsTenant: true);
        if (options.ShowHelp)
        {
            ShowSeedHelp(output);
            return options.IsValid ? 0 : 1;
        }

        var database = new MongoClient(options.ConnectionString).GetDatabase(options.Database);
        var result = await DeveloperCommands.SeedTenantRoot(database, options.Tenant!, options.Email!, options.Apply);
        return Report(result, options.Apply, output, error);
    }

    /// <returns>0 when it ran, 1 when it was asked wrongly or refused.</returns>
    public static async Task<int> GrantDeveloper(string[] args, TextWriter output, TextWriter error)
    {
        var options = Parse(args, error, needsTenant: false);
        if (options.ShowHelp)
        {
            ShowGrantHelp(output);
            return options.IsValid ? 0 : 1;
        }

        var database = new MongoClient(options.ConnectionString).GetDatabase(options.Database);
        var result = await DeveloperCommands.GrantDeveloper(database, options.Email!, options.Apply);
        return Report(result, options.Apply, output, error);
    }

    static int Report(DeveloperResult result, bool apply, TextWriter output, TextWriter error)
    {
        if (result.Refused)
        {
            error.WriteLine(result.Message);
            return 1;
        }

        output.WriteLine(apply ? "Applied." : "Dry run.");
        output.WriteLine(result.Message);
        if (!apply && result.Outcome == DeveloperOutcome.WouldChange)
        {
            output.WriteLine("Nothing was changed. Run again with --apply to persist.");
        }

        return 0;
    }

    static Options Parse(string[] args, TextWriter error, bool needsTenant)
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
                case "--email" when i + 1 < args.Length:
                    options.Email = args[++i];
                    break;
                case "--tenant" when needsTenant && i + 1 < args.Length:
                    options.Tenant = args[++i];
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

        if (!options.ShowHelp)
        {
            Require(options.ConnectionString, "--connection-string", options, error);
            Require(options.Email, "--email", options, error);
            if (needsTenant)
            {
                Require(options.Tenant, "--tenant", options, error);
            }
        }

        return options;
    }

    static void Require(string? value, string name, Options options, TextWriter error)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            options.ShowHelp = true;
            options.IsValid = false;
            error.WriteLine($"{name} is required.");
        }
    }

    static void ShowSeedHelp(TextWriter output)
    {
        output.WriteLine(
            """
            Usage:
              dotnet run --project tools/NTS.Tools -- seed-tenant-root --connection-string <mongo> --tenant <id> --email <email> [--database nts] [--apply]

            Makes the account with that exact email a Tenant Root of the Tenant (an id such as country-bg), which makes the
            Tenant operational: able to hold Events. The Tenant is made when the first person of its country registers and
            the account has to exist already. Running it again changes nothing, and nothing else of the account is touched.
            A dry run says what it would do.

            Options:
              --connection-string <mongo>   MongoDB connection string.
              --tenant <id>                 The id of the Tenant, such as country-bg.
              --email <email>               The exact email of the account.
              --database <name>             Database name. Defaults to nts.
              --apply                       Persist the change. Omit for a dry run.
            """
        );
    }

    static void ShowGrantHelp(TextWriter output)
    {
        output.WriteLine(
            """
            Usage:
              dotnet run --project tools/NTS.Tools -- grant-developer --connection-string <mongo> --email <email> [--database nts] [--apply]

            Makes the account with that exact email the Developer, the platform owner who holds every right across Tenants
            except acting as the Main Operator of a Live Event. The account has to exist already. Running it again changes
            nothing. A dry run says what it would do.

            Options:
              --connection-string <mongo>   MongoDB connection string.
              --email <email>               The exact email of the account.
              --database <name>             Database name. Defaults to nts.
              --apply                       Persist the change. Omit for a dry run.
            """
        );
    }

    sealed class Options
    {
        public string? ConnectionString { get; set; }
        public string Database { get; set; } = DEFAULT_DATABASE;
        public string? Email { get; set; }
        public string? Tenant { get; set; }
        public bool Apply { get; set; }
        public bool ShowHelp { get; set; }
        public bool IsValid { get; set; } = true;
    }
}
