using System.Globalization;
using MongoDB.Driver;
using NTS.Domain.Enums;

namespace NTS.Tools.Staging;

/// <summary>The command line of <c>seed-staging</c>. See the README of the tools for what it makes and how to run it on a hosted database.</summary>
public static class StagingSeedTool
{
    const string DEFAULT_DATABASE = "nts";

    public static Task<int> Run(string[] args)
    {
        return Run(args, Console.Out, Console.Error);
    }

    /// <param name="now">The instant the Event is made at, which is the clock of the machine unless a test gives one.</param>
    /// <returns>0 when it ran, 1 when it was asked wrongly or refused.</returns>
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
        var report = await StagingSeed.Run(
            database,
            new StagingSeedOptions
            {
                Apply = options.Apply,
                Environment = options.Environment,
                TenantRoot = options.TenantRoot,
                MainOperator = options.MainOperator,
                Officials = options.Officials,
                Operators = options.Operators,
                EventName = options.EventName,
                Days = options.Days,
                StartTime = options.StartTime,
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
                case "--tenant-root" when i + 1 < args.Length:
                    options.TenantRoot = args[++i];
                    break;
                case "--main-operator" when i + 1 < args.Length:
                    options.MainOperator = args[++i];
                    break;
                case "--operator" when i + 1 < args.Length:
                    options.Operators.Add(args[++i]);
                    break;
                case "--official" when i + 1 < args.Length:
                    Official(args[++i], options, error);
                    break;
                case "--event-name" when i + 1 < args.Length:
                    options.EventName = args[++i];
                    break;
                case "--days" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], NumberStyles.None, CultureInfo.InvariantCulture, out var days))
                    {
                        options.Fail(error, $"--days is a whole number, not '{args[i]}'.");
                    }
                    else
                    {
                        options.Days = days;
                    }

                    break;
                case "--start" when i + 1 < args.Length:
                    if (!TimeSpan.TryParseExact(args[++i], @"hh\:mm", CultureInfo.InvariantCulture, out var start))
                    {
                        options.Fail(error, $"--start is a time of the day as HH:mm, not '{args[i]}'.");
                    }
                    else
                    {
                        options.StartTime = start;
                    }

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

        if (string.IsNullOrWhiteSpace(options.ConnectionString) && !options.ShowHelp)
        {
            options.Fail(error, "--connection-string is required.");
        }

        return options;
    }

    /// <summary>An Official as <c>email:Role</c>; a role that is none of ours is a mistake to be told of, not one to be guessed at.</summary>
    static void Official(string value, Options options, TextWriter error)
    {
        var at = value.LastIndexOf(':');
        if (
            at <= 0
            || !Enum.TryParse<OfficialRole>(value[(at + 1)..], ignoreCase: true, out var role)
            || !Enum.IsDefined(role)
        )
        {
            options.Fail(
                error,
                $"--official is an email and a role, such as steward@example.org:Steward. The roles are {string.Join(", ", Enum.GetNames<OfficialRole>())}."
            );
            return;
        }

        options.Officials.Add((value[..at], role));
    }

    static void ShowHelp(TextWriter output)
    {
        output.WriteLine(
            """
            Usage:
              dotnet run --project tools/NTS.Tools -- seed-staging --connection-string <mongo> --tenant-root <email> --main-operator <email>
                  [--official <email>:<Role>]... [--operator <email>]... [--environment Staging|Development] [--database nts]
                  [--event-name <name>] [--days 7] [--start 00:00] [--apply]

            Makes, in the Tenant of Bulgaria, what a person who tries the platform on a staging database needs: a Tenant Root (which
            makes the Tenant operational), a Main Operator, Officials and Operators, and a Live Event with twelve Participations that
            the Officials and Operators may send Snapshots to. The accounts are named by their email and made when they are not there:
            each person signs in with a code sent to the address. A dry run reports all of it and changes nothing. It refuses a
            database that is marked Production, and one with no marker unless --environment says which it is (which marks it), and it
            never marks a database as Production. Running it again makes nothing twice.

            Options:
              --connection-string <mongo>   MongoDB connection string. Never paste it anywhere but the command line.
              --tenant-root <email>         The Tenant Root of the Tenant of Bulgaria.
              --main-operator <email>       The Main Operator of the Event.
              --official <email>:<Role>     An Official of the Event: Steward, ChiefSteward, GroundJury, GroundJuryPresident and the other roles.
              --operator <email>            An Operator of the Event.
              --environment <name>          Staging or Development: marks a database that has no marker.
              --database <name>             Database name. Defaults to nts.
              --event-name <name>           What the Event is called, and what makes it the same Event on every run.
              --days <n>                    The Event is Live for n days from today. Defaults to 7.
              --start <HH:mm>               The time of the day the competition starts, in UTC. Defaults to 00:00.
              --apply                       Persist changes. Omit for a dry run.
            """
        );
    }

    sealed class Options
    {
        public string? ConnectionString { get; set; }
        public string Database { get; set; } = DEFAULT_DATABASE;
        public string? Environment { get; set; }
        public string? TenantRoot { get; set; }
        public string? MainOperator { get; set; }
        public List<(string Email, OfficialRole Role)> Officials { get; } = [];
        public List<string> Operators { get; } = [];
        public string EventName { get; set; } = "Staging Seed Event";
        public int Days { get; set; } = 7;
        public TimeSpan StartTime { get; set; } = TimeSpan.Zero;
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
