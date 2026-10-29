using NTS.Domain.Enums;

namespace NTS.Tools.Staging;

/// <summary>What <c>seed-staging</c> was asked to make (#607).</summary>
public sealed class StagingSeedOptions
{
    /// <summary>Whether to write. Without it the command only reports what it would make.</summary>
    public bool Apply { get; init; }

    /// <summary>The environment to mark an unmarked database with, Staging or Development: never Production, which nothing is seeded into.</summary>
    public string? Environment { get; init; }

    /// <summary>The email of the account that is the Tenant Root of the Tenant of Bulgaria, which makes the Tenant operational.</summary>
    public string? TenantRoot { get; init; }

    /// <summary>The email of the account that is the Main Operator of the Event.</summary>
    public string? MainOperator { get; init; }

    /// <summary>The accounts that are Officials of the Event, with their role. An Official of a role that sends Snapshots may send them.</summary>
    public IReadOnlyList<(string Email, OfficialRole Role)> Officials { get; init; } = [];

    /// <summary>The accounts that are Operators of the Event, who may send Snapshots.</summary>
    public IReadOnlyList<string> Operators { get; init; } = [];

    /// <summary>What the Event is called. The Event is told from others by it: the same name is the same Event.</summary>
    public string EventName { get; init; } = "Staging Seed Event";

    /// <summary>For how many days from today the Event is Live, today included.</summary>
    public int Days { get; init; } = 7;

    /// <summary>The time of the day the competition starts, in UTC: what a Snapshot is placed after.</summary>
    public TimeSpan StartTime { get; init; } = TimeSpan.Zero;
}

/// <summary>
/// What <c>seed-staging</c> found and made (#607). A dry run and an apply report the same: what would be made and what was.
/// Nothing in it is an email or a name: accounts are told by their ids, the Event by its id.
/// </summary>
public sealed class StagingSeedReport
{
    readonly List<string> _refusals = [];

    public StagingSeedReport(bool apply)
    {
        IsApply = apply;
    }

    public bool IsApply { get; }
    public bool Applied { get; internal set; }

    /// <summary>Whether it was asked to write and was refused: a dry run only lists what would stop an apply.</summary>
    public bool Refused => _refusals.Count > 0 && IsApply;

    public IReadOnlyList<string> Refusals => _refusals;

    public string? MarkedAs { get; internal set; }
    public string? MarkWith { get; internal set; }
    public bool CountryMade { get; internal set; }
    public bool TenantMade { get; internal set; }
    public bool TenantCompleted { get; internal set; }

    /// <summary>The ids of the accounts that are made, and how many there already were.</summary>
    public IReadOnlyList<Guid> AccountsMade { get; internal set; } = [];

    public int AccountsKept { get; internal set; }
    public bool TenantRootMade { get; internal set; }
    public Guid EventId { get; internal set; }
    public bool SetupMade { get; internal set; }
    public bool CoreMade { get; internal set; }

    /// <summary>The documents the Event gets, by collection, of the ones it has none of yet.</summary>
    public IReadOnlyDictionary<string, int> EventDocuments { get; internal set; } = new Dictionary<string, int>();

    public int GrantsMade { get; internal set; }
    public DateTimeOffset EventEnds { get; internal set; }

    internal void Refuse(string reason)
    {
        _refusals.Add(reason);
    }

    public void WriteTo(TextWriter output)
    {
        output.WriteLine(IsApply ? "Seeding staging." : "Dry-run staging seed.");
        output.WriteLine(
            MarkedAs != null ? $"Environment marker: the database says {MarkedAs}."
            : MarkWith != null ? $"Environment marker: none, to be written as {MarkWith}."
            : "Environment marker: none. Name Staging or Development with --environment."
        );
        output.WriteLine($"country: Bulgaria {(CountryMade ? "to make" : "is there")}");
        output.WriteLine(
            $"tenant: country-bg {(TenantMade ? "to make" : TenantCompleted ? "to complete" : "is there")}"
        );
        output.WriteLine(
            $"accounts: {AccountsMade.Count} to make, {AccountsKept} there already, the Tenant Root {(TenantRootMade ? "to make" : "is one already")}"
        );
        foreach (var account in AccountsMade)
        {
            output.WriteLine($"  users {account}");
        }

        output.WriteLine(
            $"event {EventId}: Setup {(SetupMade ? "to make" : "is there")}, Event {(CoreMade ? "to make" : "is there")}, Live until {EventEnds:yyyy-MM-dd HH:mm:ss}Z"
        );
        foreach (var (collection, count) in EventDocuments)
        {
            output.WriteLine($"  {collection}: {count} to make");
        }

        output.WriteLine($"event_grants: {GrantsMade} to make");
        foreach (var reason in Refusals)
        {
            output.WriteLine($"{(IsApply ? "Refused" : "An apply would be refused")}: {reason}");
        }

        output.WriteLine(
            Applied ? "Applied."
            : Refused ? "Nothing was changed: refused."
            : "Nothing was changed. Run again with --apply to persist."
        );
    }
}
