using System.Text.RegularExpressions;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Not.Identity;
using NoTiming.Api.Features.Reference;
using NTS.Contracts.Core.Models;
using NTS.Domain.Access;
using NTS.Domain.Aggregates;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>An Event as the Api needs to know it to decide who may do what: where it belongs, who runs it, and when it ends.</summary>
internal sealed class EventRecord
{
    public EventRecord(Guid id, string tenantId, Guid? mainOperatorId, bool isStarted, DateTimeOffset? endDay)
    {
        Id = id;
        TenantId = tenantId;
        MainOperatorId = mainOperatorId;
        IsStarted = isStarted;
        EndDay = endDay;
    }

    public Guid Id { get; }
    public string TenantId { get; }
    public Guid? MainOperatorId { get; }

    /// <summary>Whether the Event has been made from its Setup, which is whether a Core document of it exists.</summary>
    public bool IsStarted { get; }

    /// <summary>The end of the last day of a started Event, as an instant. An Event that has not started has none.</summary>
    public DateTimeOffset? EndDay { get; }

    public EventStage StageAt(DateTimeOffset now)
    {
        return EventStageRule.Of(IsStarted, EndDay, now);
    }
}

/// <summary>
/// A row of a registry found across Tenants: the fields that are meant to leave, and the Tenant that owns the row. The
/// account an Athlete is linked to, and anything else the document has, never does.
/// </summary>
internal sealed class RegistryMatch
{
    public RegistryMatch(
        Guid id,
        string tenantId,
        string name,
        string? nameEnglish,
        string? feiId,
        string? club,
        string? country,
        string? role
    )
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        NameEnglish = nameEnglish;
        FeiId = feiId;
        Club = club;
        Country = country;
        Role = role;
    }

    public Guid Id { get; }
    public string TenantId { get; }
    public string Name { get; }
    public string? NameEnglish { get; }
    public string? FeiId { get; }
    public string? Club { get; }
    public string? Country { get; }
    public string? Role { get; }
}

/// <summary>The policy refused a read across Tenants that its route should have refused first.</summary>
internal sealed class CrossTenantRefusedException : InvalidOperationException
{
    public CrossTenantRefusedException(string read, Refusal reason)
        : base($"The read '{read}' across Tenants was refused ({reason}): its route has to check the policy first.") { }
}

/// <summary>
/// What reaches across Tenants, each read a named capability of a collection and never a flag of a general one
/// (ADR-0012, #643): an Event by its id, the Events a Main Operator runs, and the search of the registries of Athletes,
/// Horses, Clubs and Officials. They are the only reads that open a collection of a Tenant without one: each takes only
/// what it needs, matches the text it is given literally, answers at most ten rows with only the fields that are meant to
/// leave, and the searches ask the policy themselves as a second guard behind their routes. Reading an Event is public
/// (ADR-0001), so an Event's id is what finds it; what the registries hold is for signed-in accounts alone.
/// </summary>
internal sealed class CrossTenantReads
{
    const string STANDARD_ID = "_id";
    public const int MIN_SEARCH_LENGTH = 3;
    public const int MAX_MATCHES = 10;

    readonly IMongoDatabase _database;

    public CrossTenantReads(IMongoClient client, IOptions<NIdentityOptions> options)
    {
        _database = client.GetDatabase(options.Value.Database);
    }

    /// <summary>
    /// The Event with the id, from the Core once it has started and from the Setup before. None when there is no such
    /// Event. A document from before Tenants has the constant Tenant and nobody to run it. Only the fields that say who
    /// may do what are read: a Setup holds the whole configuration of the Event.
    /// </summary>
    public async Task<EventRecord?> FindEventAsync(Guid id, CancellationToken cancellationToken)
    {
        var key = new BsonDocument(STANDARD_ID, BsonGuids.Binary(id));
        var core = await Raw(TenantOwned.EVENT_INFORMATIONS)
            .Find(key)
            .Project(EventFields())
            .FirstOrDefaultAsync(cancellationToken);
        var source =
            core
            ?? await Raw(TenantOwned.CONFIGURE_EVENTS)
                .Find(key)
                .Project(EventFields())
                .FirstOrDefaultAsync(cancellationToken);
        if (source == null)
        {
            return null;
        }

        return new EventRecord(
            id,
            TextOf(source, TenantOwned.TENANT_ID) ?? Tenant.LEGACY_ID,
            BsonGuids.Of(source, "MainOperatorId"),
            core != null,
            core != null && core.TryGetValue("EndDay", out var end) && end.IsBsonDateTime
                ? new DateTimeOffset(end.ToUniversalTime(), TimeSpan.Zero)
                : null
        );
    }

    /// <summary>
    /// The Events that have started, of every Tenant, as the public views show them (ADR-0012: a Tenant organises data and
    /// does not wall it off, and anybody may read an Event): the ones that are Live at the instant, the ones that are
    /// Historic, or all, filtered and sorted as the options say. The caller says what it shows of each.
    /// </summary>
    public async Task<IReadOnlyList<EventInformationModel>> ReadPublicEventsAsync(
        EventStage? stage,
        DateTimeOffset now,
        ODataQueryOptions<EventInformationModel>? options,
        int skip,
        int take,
        CancellationToken cancellationToken
    )
    {
        IQueryable<EventInformationModel> events = _database
            .GetCollection<EventInformationModel>(TenantOwned.EVENT_INFORMATIONS)
            .AsQueryable();
        if (stage == EventStage.Live)
        {
            events = events.Where(x => x.EndDay > now);
        }
        else if (stage == EventStage.Historic)
        {
            events = events.Where(x => x.EndDay <= now);
        }

        return await ReferencePages.ReadAsync(events, options, skip, take, cancellationToken);
    }

    /// <summary>A started Event by its id, whatever Tenant it is in, as the public views show it; none when it has not started.</summary>
    public async Task<EventInformationModel?> FindPublicEventAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _database
            .GetCollection<EventInformationModel>(TenantOwned.EVENT_INFORMATIONS)
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// The document of the grant with the id, whatever Tenant it is in: a grant is found by its id to learn which Event it
    /// belongs to, and what may be done with it is then asked of that Event. None when there is no such grant.
    /// </summary>
    public async Task<BsonDocument?> FindGrantAsync(Guid id, CancellationToken cancellationToken)
    {
        return await Raw(TenantOwned.EVENT_GRANTS)
            .Find(new BsonDocument(STANDARD_ID, BsonGuids.Binary(id)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// The Tenants of the Events the account runs that are not yet Historic: the ones it was made the Main Operator of that
    /// have not started, and the started ones it holds now that have not ended. A started Event is the Core's, so what
    /// the Setup says of its Main Operator does not count once it has started.
    /// </summary>
    public async Task<IReadOnlyList<string>> OpenEventTenantsOfAsync(
        Guid mainOperatorId,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var mine = new BsonDocument("MainOperatorId", BsonGuids.Binary(mainOperatorId));
        var setups = await Raw(TenantOwned.CONFIGURE_EVENTS)
            .Find(mine)
            .Project(EventFields())
            .ToListAsync(cancellationToken);
        var cores = await Raw(TenantOwned.EVENT_INFORMATIONS)
            .Find(mine)
            .Project(EventFields())
            .ToListAsync(cancellationToken);

        var started = new HashSet<BsonValue>();
        if (setups.Count > 0)
        {
            var ids = new BsonArray(setups.Select(x => x[STANDARD_ID]));
            var hasCore = await Raw(TenantOwned.EVENT_INFORMATIONS)
                .Find(new BsonDocument(STANDARD_ID, new BsonDocument("$in", ids)))
                .Project(new BsonDocument(STANDARD_ID, 1))
                .ToListAsync(cancellationToken);
            started.UnionWith(hasCore.Select(x => x[STANDARD_ID]));
        }

        var tenants = new HashSet<string>(StringComparer.Ordinal);
        foreach (var setup in setups.Where(x => !started.Contains(x[STANDARD_ID])))
        {
            tenants.Add(TextOf(setup, TenantOwned.TENANT_ID) ?? Tenant.LEGACY_ID);
        }

        foreach (var core in cores)
        {
            var end =
                core.TryGetValue("EndDay", out var value) && value.IsBsonDateTime
                    ? new DateTimeOffset(value.ToUniversalTime(), TimeSpan.Zero)
                    : (DateTimeOffset?)null;
            if (EventStageRule.Of(true, end, now) == EventStage.Live)
            {
                tenants.Add(TextOf(core, TenantOwned.TENANT_ID) ?? Tenant.LEGACY_ID);
            }
        }

        return [.. tenants];
    }

    public Task<IReadOnlyList<RegistryMatch>> SearchAthletesAsync(
        Caller caller,
        string text,
        CancellationToken cancellationToken
    )
    {
        return SearchAsync("athletes", "athletes", ["Name", "NameEnglish", "FeiId"], caller, text, cancellationToken);
    }

    public Task<IReadOnlyList<RegistryMatch>> SearchHorsesAsync(
        Caller caller,
        string text,
        CancellationToken cancellationToken
    )
    {
        return SearchAsync("horses", "horses", ["Name", "NameEnglish", "FeiId"], caller, text, cancellationToken);
    }

    public Task<IReadOnlyList<RegistryMatch>> SearchClubsAsync(
        Caller caller,
        string text,
        CancellationToken cancellationToken
    )
    {
        return SearchAsync("clubs", "clubs", ["Name"], caller, text, cancellationToken);
    }

    /// <summary>
    /// The Officials of any Event, by name, once each: the same name in the same role in many Events is one row. They are
    /// the Officials of the Events that have started, as the Core keeps them.
    /// </summary>
    public async Task<IReadOnlyList<RegistryMatch>> SearchOfficialsAsync(
        Caller caller,
        string text,
        CancellationToken cancellationToken
    )
    {
        var filter = Matching(["Name", "NameEnglish"], Needle("officials", caller, text));
        var rows = await Raw("event_officials")
            .Aggregate()
            .Match(filter)
            .Group(
                new BsonDocument
                {
                    {
                        STANDARD_ID,
                        new BsonDocument
                        {
                            { "Name", "$Name" },
                            { "NameEnglish", "$NameEnglish" },
                            { "Role", "$Role" },
                        }
                    },
                    { "Id", new BsonDocument("$first", "$_id") },
                    { "TenantId", new BsonDocument("$first", "$TenantId") },
                }
            )
            .Sort(new BsonDocument { { "_id.Name", 1 }, { "_id.Role", 1 } })
            .Limit(MAX_MATCHES)
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(x => (Group: x[STANDARD_ID].AsBsonDocument, Row: x))
                .Where(x => x.Row.TryGetValue("Id", out var id) && id.IsBsonBinaryData)
                .Select(x => new RegistryMatch(
                    x.Row["Id"].AsGuid,
                    TextOf(x.Row, TenantOwned.TENANT_ID) ?? Tenant.LEGACY_ID,
                    TextOf(x.Group, "Name") ?? string.Empty,
                    TextOf(x.Group, "NameEnglish"),
                    null,
                    null,
                    null,
                    TextOf(x.Group, "Role")
                )),
        ];
    }

    async Task<IReadOnlyList<RegistryMatch>> SearchAsync(
        string read,
        string collection,
        string[] fields,
        Caller caller,
        string text,
        CancellationToken cancellationToken
    )
    {
        var filter = Matching(fields, Needle(read, caller, text));
        var rows = await Raw(collection)
            .Find(filter)
            .Project(
                new BsonDocument
                {
                    { STANDARD_ID, 1 },
                    { TenantOwned.TENANT_ID, 1 },
                    { "Name", 1 },
                    { "NameEnglish", 1 },
                    { "FeiId", 1 },
                    { "Club.Name", 1 },
                    { "Country.Name", 1 },
                }
            )
            .Sort(new BsonDocument("Name", 1))
            .Limit(MAX_MATCHES)
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Where(x => x.TryGetValue(STANDARD_ID, out var id) && id.IsBsonBinaryData)
                .Select(x => new RegistryMatch(
                    x[STANDARD_ID].AsGuid,
                    TextOf(x, TenantOwned.TENANT_ID) ?? Tenant.LEGACY_ID,
                    TextOf(x, "Name") ?? string.Empty,
                    TextOf(x, "NameEnglish"),
                    TextOf(x, "FeiId"),
                    NestedTextOf(x, "Club", "Name"),
                    NestedTextOf(x, "Country", "Name"),
                    null
                )),
        ];
    }

    /// <summary>
    /// The text to look for, once the policy has allowed the read and the text is long enough. A search for less than a
    /// few characters would be a list, and a list is what the registries are not.
    /// </summary>
    static string Needle(string read, Caller caller, string text)
    {
        var verdict = AccessPolicy.Decide(Capability.ReadRegistryAcrossTenants, caller, AccessScope.Platform);
        if (!verdict.IsAllowed)
        {
            throw new CrossTenantRefusedException(read, verdict.Reason!.Value);
        }

        var needle = text.Trim();
        return needle.Length < MIN_SEARCH_LENGTH
            ? throw new ArgumentException($"A search is for at least {MIN_SEARCH_LENGTH} characters.", nameof(text))
            : needle;
    }

    /// <summary>Any of the fields contains the text, in any case, as it is written: it is never a pattern.</summary>
    static FilterDefinition<BsonDocument> Matching(string[] fields, string needle)
    {
        var pattern = new BsonRegularExpression(Regex.Escape(needle), "i");
        return Builders<BsonDocument>.Filter.Or(fields.Select(x => Builders<BsonDocument>.Filter.Regex(x, pattern)));
    }

    IMongoCollection<BsonDocument> Raw(string collection)
    {
        return _database.GetCollection<BsonDocument>(collection);
    }

    /// <summary>What an Event's document is read for here: where it belongs, who runs it and when it ends.</summary>
    static BsonDocument EventFields()
    {
        return new BsonDocument
        {
            { STANDARD_ID, 1 },
            { TenantOwned.TENANT_ID, 1 },
            { "MainOperatorId", 1 },
            { "EndDay", 1 },
        };
    }

    static string? TextOf(BsonDocument document, string field)
    {
        return
            document.TryGetValue(field, out var value) && value.IsString && !string.IsNullOrWhiteSpace(value.AsString)
            ? value.AsString
            : null;
    }

    static string? NestedTextOf(BsonDocument document, string field, string nested)
    {
        return document.TryGetValue(field, out var value) && value.IsBsonDocument
            ? TextOf(value.AsBsonDocument, nested)
            : null;
    }
}
