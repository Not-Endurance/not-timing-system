namespace NTS.Tools.Tenants;

/// <summary>What <c>migrate-tenants</c> was asked to do (#607).</summary>
public sealed class TenantsOptions
{
    /// <summary>Whether to write. Without it the command only reports what it would do.</summary>
    public bool Apply { get; init; }

    /// <summary>The environment the database is, as it was typed: the marker is written with it, and an apply needs it.</summary>
    public string? Environment { get; init; }

    /// <summary>The email of the account that becomes the Main Operator of the Events that have none; the Tenant Root when it is not given.</summary>
    public string? MainOperatorEmail { get; init; }
}

/// <summary>
/// What <c>migrate-tenants</c> found and did (#607, ADR-0012). A dry run and an apply report the same numbers: what the apply
/// would write and what it wrote. Nothing in it is an email, a name or anything else an account keeps: documents are told by
/// their ids and the Tenants by their keys.
/// </summary>
public sealed class TenantsReport
{
    readonly List<string> _refusals = [];
    readonly List<IReadOnlyList<Guid>> _duplicateEmails = [];
    readonly Dictionary<string, int> _placed = [];
    readonly Dictionary<string, long> _stamped = [];

    public TenantsReport(bool apply)
    {
        IsApply = apply;
    }

    /// <summary>Whether it was asked to write.</summary>
    public bool IsApply { get; }

    /// <summary>Whether it wrote: asked to, and not refused.</summary>
    public bool Applied { get; internal set; }

    public bool Refused => _refusals.Count > 0 && IsApply;

    /// <summary>Why an apply was refused, each with what to do about it. A dry run only lists them.</summary>
    public IReadOnlyList<string> Refusals => _refusals;

    /// <summary>The name the database is marked with before the run, none when it has no marker.</summary>
    public string? MarkedAs { get; internal set; }

    /// <summary>The environment the marker will say (or says now), as it is written; none when none was named.</summary>
    public string? MarkWith { get; internal set; }

    public int Countries { get; internal set; }
    public IReadOnlyList<Guid> CountriesWithoutIsoCode { get; internal set; } = [];
    public bool HasBulgaria { get; internal set; }

    public int Accounts { get; internal set; }
    public IReadOnlyList<Guid> AccountsWithoutEmail { get; internal set; } = [];

    /// <summary>The accounts whose id is not a UUID of the standard kind, which Identity cannot read: they are left as they are until their ids are converted.</summary>
    public int AccountsWithOtherIds { get; internal set; }

    /// <summary>The ids of the accounts that share an email, in groups, whatever the case of what was typed.</summary>
    public IReadOnlyList<IReadOnlyList<Guid>> DuplicateEmails => _duplicateEmails;

    /// <summary>The accounts that are changed in any way: the fields of Identity, an email that is written in its form, a home Tenant.</summary>
    public int AccountsCompleted { get; internal set; }

    public int EmailsNormalized { get; internal set; }

    /// <summary>The accounts placed in a Tenant, by the Tenant.</summary>
    public IReadOnlyDictionary<string, int> Placed => _placed;

    /// <summary>The accounts that have no home Tenant and get none: no country in the profile, or one that is none of ours.</summary>
    public int Unplaced { get; internal set; }

    public IReadOnlyList<string> TenantsMade { get; internal set; } = [];
    public IReadOnlyList<string> TenantsCompleted { get; internal set; } = [];

    /// <summary>The documents of each collection a Tenant owns that are stamped with the Tenant of Bulgaria.</summary>
    public IReadOnlyDictionary<string, long> Stamped => _stamped;

    public int EventsNotHistoric { get; internal set; }

    /// <summary>The Events that were given their Tenant Root, or the account that was named, as their Main Operator.</summary>
    public int MainOperatorsAssigned { get; internal set; }

    /// <summary>The Events that are not Historic and have no Main Operator because nobody could be told to be it.</summary>
    public IReadOnlyList<Guid> EventsWaitingForMainOperator { get; internal set; } = [];

    /// <summary>Why they wait, and what to do.</summary>
    public string? MainOperatorNote { get; internal set; }

    /// <summary>The started Events whose last day is not a date: they cannot be told from Historic ones and are taken to be over.</summary>
    public IReadOnlyList<Guid> EventsWithoutAnEnd { get; internal set; } = [];

    /// <summary>The documents of Events whose id is not a UUID of the standard kind (not converted yet): they wait for a run after the ids are.</summary>
    public int EventsWithOtherIds { get; internal set; }

    public int GrantsMade { get; internal set; }
    public int GrantsPending { get; internal set; }

    /// <summary>The copies of an Official or an Operator that name an account that is not there: nobody to give a grant to.</summary>
    public int GrantsDangling { get; internal set; }

    public int SessionsRekeyed { get; internal set; }
    public int SessionsKept { get; internal set; }

    public long SettingsDocuments { get; internal set; }
    public bool SettingsDropped { get; internal set; }

    /// <summary>The indexes that were not there and are made.</summary>
    public IReadOnlyList<string> IndexesMade { get; internal set; } = [];

    internal void Refuse(string reason)
    {
        _refusals.Add(reason);
    }

    internal void AddDuplicates(IReadOnlyList<Guid> accounts)
    {
        _duplicateEmails.Add(accounts);
    }

    internal void Place(string tenant)
    {
        _placed[tenant] = _placed.GetValueOrDefault(tenant) + 1;
    }

    internal void Stamp(string collection, long documents)
    {
        _stamped[collection] = documents;
    }

    public void WriteTo(TextWriter output)
    {
        output.WriteLine(IsApply ? "Applying tenants migration." : "Dry-run tenants migration.");
        output.WriteLine(
            MarkedAs != null ? $"Environment marker: the database says {MarkedAs}."
            : MarkWith != null ? $"Environment marker: none, to be written as {MarkWith}."
            : "Environment marker: none. Name the environment with --environment to write one."
        );
        output.WriteLine(
            Countries == 0
                ? "countries: no countries, so no Tenant can be made."
                : $"countries: {Countries} documents read, {(HasBulgaria ? "Bulgaria (BG) is there" : "Bulgaria (BG) is missing")}, {CountriesWithoutIsoCode.Count} without an ISO code"
        );
        foreach (var country in CountriesWithoutIsoCode)
        {
            output.WriteLine($"  countries {country}: no ISO code, so no Tenant is made for it");
        }

        output.WriteLine(
            $"users: {Accounts} documents read, {Accounts - AccountsWithoutEmail.Count} with an email, {AccountsWithoutEmail.Count} without one, {AccountsCompleted} to complete ({EmailsNormalized} emails to write in lower case)"
        );
        foreach (var account in AccountsWithoutEmail)
        {
            output.WriteLine($"  users {account}: no email, left as it is");
        }

        if (AccountsWithOtherIds > 0)
        {
            output.WriteLine(
                $"  users: {AccountsWithOtherIds} documents have an id that is not a standard UUID and are left as they are until the ids are converted"
            );
        }

        output.WriteLine($"Accounts that share an email: {DuplicateEmails.Count} emails");
        foreach (var group in DuplicateEmails)
        {
            output.WriteLine($"  users {string.Join(", ", group)}");
        }

        output.WriteLine(
            Placed.Count == 0
                ? $"Home Tenants to place: none, {Unplaced} accounts without one that stay so"
                : $"Home Tenants to place: {string.Join(", ", Placed.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key} {x.Value}"))}, {Unplaced} accounts without one that stay so"
        );
        output.WriteLine(
            $"tenants: {TenantsMade.Count} to make ({string.Join(", ", TenantsMade)}), {TenantsCompleted.Count} to complete ({string.Join(", ", TenantsCompleted)})"
        );
        foreach (var (collection, documents) in Stamped)
        {
            output.WriteLine($"{collection}: {documents} documents to stamp with country-bg");
        }

        output.WriteLine(
            $"Events that are not Historic: {EventsNotHistoric}, {MainOperatorsAssigned} to give a Main Operator, {EventsWaitingForMainOperator.Count} waiting"
        );
        if (MainOperatorNote != null)
        {
            output.WriteLine($"  {MainOperatorNote}");
        }

        foreach (var waiting in EventsWaitingForMainOperator)
        {
            output.WriteLine($"  event {waiting}: no Main Operator yet");
        }

        if (EventsWithOtherIds > 0)
        {
            output.WriteLine(
                $"  events: {EventsWithOtherIds} documents have an id that is not a standard UUID and wait for a run after the ids are converted"
            );
        }

        output.WriteLine($"Started Events whose last day is not a date: {EventsWithoutAnEnd.Count}, taken to be over");
        foreach (var unknown in EventsWithoutAnEnd)
        {
            output.WriteLine($"  event {unknown}");
        }

        output.WriteLine(
            $"event_grants: {GrantsMade} to make ({GrantsPending} pending invitations), {GrantsDangling} copies name an account that is not there"
        );
        output.WriteLine(
            $"event_user_sessions: {SessionsRekeyed} to key by the account, {SessionsKept} that stay as they are"
        );
        output.WriteLine($"settings: {SettingsDocuments} documents, {(SettingsDropped ? "dropped" : "to drop")}");
        output.WriteLine($"indexes: {IndexesMade.Count} to make ({string.Join(", ", IndexesMade)})");
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
