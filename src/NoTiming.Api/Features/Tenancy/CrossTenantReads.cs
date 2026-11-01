using Microsoft.AspNetCore.OData.Query;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Not.Identity;
using NoTiming.Api.Features.Participants;
using NoTiming.Api.Features.Reference;
using NTS.Contracts.Core.Models;
using NTS.Contracts.Features.Participants;
using NTS.Contracts.Shared;
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
/// account an Athlete is linked to, and anything else the document has, never does. The row of an Athlete or a Horse leaves whole
/// only to the people who set an Event up (<see cref="Capability.ReadRegistryDetailsAcrossTenants"/>); to any other account it
/// tells the name and the name in English, which is all it is meant to tell of a person.
/// </summary>
internal sealed class RegistryMatch
{
    /// <summary>A row that tells its name and its name in English and nothing else.</summary>
    public static RegistryMatch NameOnly(Guid id, string name, string? nameEnglish)
    {
        return new RegistryMatch(id, string.Empty, name, nameEnglish, null, null, null, null, isNameOnly: true);
    }

    public RegistryMatch(
        Guid id,
        string tenantId,
        string name,
        string? nameEnglish,
        string? feiId,
        string? club,
        string? country,
        string? role,
        bool isNameOnly = false
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
        IsNameOnly = isNameOnly;
    }

    /// <summary>Whether the row tells only its name and its name in English, as the row of a person does to an account that sets nothing up.</summary>
    public bool IsNameOnly { get; }

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
/// (ADR-0012, #643): an Event by its id, the Events a Main Operator runs, whether a Participation refers to a row of the
/// registry (#635), the search of the registries of Athletes, Horses, Clubs and Officials, and what the history of a
/// Participant, an Athlete or a Horse, is made of (#638): the Participations of the Participant, the Events and the Rankings those
/// are in. They are the only reads that open a collection of a Tenant without one: each takes only what it needs, a search
/// matches the words it is given literally and in the database (<see cref="NameSearch"/>) and answers a page of rows with only
/// the fields that are meant to leave, and the searches ask the policy themselves as a second guard behind their routes.
/// Reading an Event is public (ADR-0001), so an Event's id is what finds it; what the registries hold, and the Participations of
/// a Participant across Events, are for signed-in accounts alone.
/// </summary>
internal sealed class CrossTenantReads
{
    const string STANDARD_ID = "_id";

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
    /// A row of what an Event keeps (a Participation, a Ranking, an Official or a Handout) by its id, whatever Tenant it is in,
    /// as the public views show it (ADR-0001): the id finds it, and none when there is none. Only the collections an Event
    /// keeps can be opened this way.
    /// </summary>
    public async Task<T?> FindEventRowAsync<T>(string collection, Guid id, CancellationToken cancellationToken)
        where T : class, IDocument
    {
        if (!TenantOwned.OfAnEvent.Contains(collection))
        {
            throw new ArgumentException($"'{collection}' is not a collection that an Event keeps.", nameof(collection));
        }

        return await _database
            .GetCollection<T>(collection)
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// The Participation that holds the time event with the id, whatever Tenant it is in: a Snapshot is found by its id to
    /// learn which Event it belongs to, and what may be done with it is then asked of that Event (#644). The id of a time
    /// event is a Guid that a device made, so one that is in a Participation is not told to be anybody else's. None when no
    /// Participation holds it.
    /// </summary>
    public async Task<ParticipationModel?> FindParticipationOfTimeEventAsync(
        Guid timeEventId,
        CancellationToken cancellationToken
    )
    {
        return await _database
            .GetCollection<ParticipationModel>(TenantOwned.EVENT_PARTICIPATIONS)
            .Find(new BsonDocument("Phases.Events._id", BsonGuids.Binary(timeEventId)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// The Events, of every Tenant, that keep a Ranking some entry of which holds no placing: what the host looks at to
    /// finalise the Rankings of the Events that have ended (#640). It reads the ids of the Events and nothing else, and what
    /// is done to an Event is then asked of the Event.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> ReadEventsWithUnplacedRankingsAsync(CancellationToken cancellationToken)
    {
        var unplaced = new BsonDocument(
            "Entries",
            new BsonDocument("$elemMatch", new BsonDocument("Rank", BsonNull.Value))
        );
        var ids = await (
            await Raw(TenantOwned.EVENT_RANKINGS)
                .DistinctAsync<BsonValue>("EventId", unplaced, cancellationToken: cancellationToken)
        ).ToListAsync(cancellationToken);
        return
        [
            .. ids.Where(x => x is BsonBinaryData { SubType: BsonBinarySubType.UuidStandard })
                .Select(x => x.AsBsonBinaryData.ToGuid(GuidRepresentation.Standard)),
        ];
    }

    /// <summary>
    /// Whether any Participation, of any Tenant, of an Event that has started or of one that has not, refers to the Athlete or the
    /// Horse with the id (#635, ADR-0010): the copies a Participation keeps carry the id of the registry row they come from, so
    /// a row that is named is the one thing that ties them together. It answers whether and nothing about what or whose, and the
    /// route that asks it has found the row among the caller's own first.
    /// </summary>
    /// <param name="member">The member of the combination that holds the copy: <c>Athlete</c> or <c>Horse</c>.</param>
    public async Task<bool> IsReferencedByParticipationAsync(
        string member,
        Guid id,
        CancellationToken cancellationToken
    )
    {
        var key = BsonGuids.Binary(id);
        var started = await Raw(TenantOwned.EVENT_PARTICIPATIONS)
            .Find(new BsonDocument($"Combination.{member}.{STANDARD_ID}", key))
            .Limit(1)
            .AnyAsync(cancellationToken);
        return started
            || await Raw(TenantOwned.CONFIGURE_EVENTS)
                .Find(new BsonDocument($"Competitions.Participations.Combination.{member}.{STANDARD_ID}", key))
                .Limit(1)
                .AnyAsync(cancellationToken);
    }

    /// <summary>
    /// A page of the rides of the Athlete or the Horse with the id, in any Tenant and of the Events that have ended by the
    /// instant (#638, ADR-0010): the copies a Participation keeps carry the id of the registry row they come from, so the id
    /// finds the rides of a person whatever Tenant ran them, and the database joins each with its Event, filters and sorts the
    /// rows as the options say (the newest Event first when they do not sort) and cuts the page, so that only the rides on it are
    /// read. A Participation or an Event that the Functions host flagged as deleted is not one. Reading across the Events of a
    /// Participant is for signed-in accounts alone, and the policy is asked here as a second guard behind the route.
    /// </summary>
    public async Task<IReadOnlyList<HistoryRide>> ReadHistoryAsync(
        Caller caller,
        ParticipantKind kind,
        Guid id,
        DateTimeOffset now,
        ODataQueryOptions<HistoryRide>? options,
        int skip,
        int take,
        CancellationToken cancellationToken
    )
    {
        Allow("history of a Participant", caller);
        var participations = _database
            .GetCollection<ParticipationModel>(TenantOwned.EVENT_PARTICIPATIONS)
            .AsQueryable()
            .Where(x => !x.IsDeleted);
        var rides =
            kind == ParticipantKind.Athlete
                ? participations.Where(x => x.Combination.Athlete.Id == id)
                : participations.Where(x => x.Combination.Horse.Id == id);
        var joined = rides
            .Join(
                _database.GetCollection<EventInformationModel>(TenantOwned.EVENT_INFORMATIONS).AsQueryable(),
                ride => ride.EventId,
                happening => happening.Id,
                (ride, happening) =>
                    new HistoryRide
                    {
                        Id = ride.Id,
                        EventId = happening.Id,
                        EventName = happening.Name,
                        Location = happening.Location,
                        StartDay = happening.StartDay,
                        EndDay = happening.EndDay,
                        Competition = ride.Competition.Name,
                        Ruleset = ride.Competition.Ruleset.ToString(),
                        Category = ride.Category.ToString(),
                        AthleteId = ride.Combination.Athlete.Id,
                        HorseId = ride.Combination.Horse.Id,
                        EventIsDeleted = happening.IsDeleted,
                    }
            )
            .Where(x => !x.EventIsDeleted && x.EndDay <= now);

        return await ReferencePages.ReadAsync(
            joined,
            options,
            x =>
                x.OrderByDescending(ride => ride.EndDay)
                    .ThenByDescending(ride => ride.StartDay)
                    .ThenBy(ride => ride.Id),
            x => x.ThenBy(ride => ride.Id),
            skip,
            take,
            cancellationToken
        );
    }

    /// <summary>The Participations with the ids, whatever Tenant they are in, but for the ones the Functions host flagged as deleted.</summary>
    public async Task<IReadOnlyList<ParticipationModel>> ReadParticipationsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken
    )
    {
        return await ReadOfEventsAsync<ParticipationModel>(
            TenantOwned.EVENT_PARTICIPATIONS,
            STANDARD_ID,
            ids,
            cancellationToken
        );
    }

    /// <summary>
    /// The Events that have started with the ids, whatever Tenant they are in, as the public views show them (ADR-0001), but
    /// for the ones the Functions host flagged as deleted.
    /// </summary>
    public async Task<IReadOnlyList<EventInformationModel>> ReadStartedEventsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken
    )
    {
        return await ReadOfEventsAsync<EventInformationModel>(
            TenantOwned.EVENT_INFORMATIONS,
            STANDARD_ID,
            ids,
            cancellationToken
        );
    }

    /// <summary>
    /// The Rankings of the Events with the ids, whatever Tenant they are in, as the public views show them (ADR-0001), but for
    /// the ones the Functions host flagged as deleted.
    /// </summary>
    public async Task<IReadOnlyList<RankingModel>> ReadRankingsOfEventsAsync(
        IReadOnlyCollection<Guid> eventIds,
        CancellationToken cancellationToken
    )
    {
        return await ReadOfEventsAsync<RankingModel>(
            TenantOwned.EVENT_RANKINGS,
            "EventId",
            eventIds,
            cancellationToken
        );
    }

    /// <summary>
    /// Every Participation of the Event with the id, as the public views show them (ADR-0001), but for the ones the Functions
    /// host flagged as deleted: what a Ranking that holds no ranks is ranked over.
    /// </summary>
    public async Task<IReadOnlyList<ParticipationModel>> ReadParticipationsOfEventAsync(
        Guid eventId,
        CancellationToken cancellationToken
    )
    {
        return await ReadOfEventsAsync<ParticipationModel>(
            TenantOwned.EVENT_PARTICIPATIONS,
            "EventId",
            [eventId],
            cancellationToken
        );
    }

    /// <summary>
    /// The Athletes or the Horses with the ids, of any Tenant, as the names they were registered with (#638): what a person
    /// who has linked themselves to rows of the registries is shown of them. An id that is no row of the registry is not in
    /// the answer.
    /// </summary>
    public async Task<IReadOnlyList<Participant>> FindParticipantsAsync(
        Caller caller,
        ParticipantKind kind,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken
    )
    {
        Allow("registry names", caller);
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await Raw(RegistryOf(kind))
            .Find(new BsonDocument(STANDARD_ID, InAny(ids)))
            .Project(ParticipantFields())
            .ToListAsync(cancellationToken);
        return [.. rows.Select(x => ParticipantOf(kind, x)).OfType<Participant>()];
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

    /// <summary>
    /// The page of Athletes whose name or name in English has the words of the search in it, and, for the people who set an Event up
    /// (<see cref="Capability.ReadRegistryDetailsAcrossTenants"/>), whose FEI ID has them too, each with the whole row. To any other
    /// account a row is its name and its name in English, and the FEI ID is not matched, so that a search cannot tell who has which.
    /// </summary>
    public Task<IReadOnlyList<RegistryMatch>> SearchAthletesAsync(
        Caller caller,
        NameSearch search,
        CancellationToken cancellationToken
    )
    {
        return SearchPeopleAsync("athletes", caller, search, cancellationToken);
    }

    /// <summary>The same for the Horses.</summary>
    public Task<IReadOnlyList<RegistryMatch>> SearchHorsesAsync(
        Caller caller,
        NameSearch search,
        CancellationToken cancellationToken
    )
    {
        return SearchPeopleAsync("horses", caller, search, cancellationToken);
    }

    public Task<IReadOnlyList<RegistryMatch>> SearchClubsAsync(
        Caller caller,
        NameSearch search,
        CancellationToken cancellationToken
    )
    {
        return SearchAsync("clubs", "clubs", ["Name"], true, caller, search, cancellationToken);
    }

    /// <summary>
    /// The Officials of any Event, by name, once each: the same name in the same role in many Events is one row. They are
    /// the Officials of the Events that have started, as the Core keeps them. The rows are the groups of the same name, name in
    /// English and role, so those three are what orders them.
    /// </summary>
    public async Task<IReadOnlyList<RegistryMatch>> SearchOfficialsAsync(
        Caller caller,
        NameSearch search,
        CancellationToken cancellationToken
    )
    {
        Admit("officials", caller, search);
        var rows = await Raw("event_officials")
            .Aggregate()
            .Match(search.Matching("Name", "NameEnglish"))
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
            .Sort(search.OrderBy("_id.Name", "_id.NameEnglish", "_id.Role"))
            .Skip(search.Skip)
            .Limit(search.Take)
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

    /// <summary>The Athletes or the Horses: the whole row for the people who set an Event up, the name for every other account.</summary>
    Task<IReadOnlyList<RegistryMatch>> SearchPeopleAsync(
        string collection,
        Caller caller,
        NameSearch search,
        CancellationToken cancellationToken
    )
    {
        var whole = AccessPolicy
            .Decide(Capability.ReadRegistryDetailsAcrossTenants, caller, AccessScope.Platform)
            .IsAllowed;
        return SearchAsync(
            collection,
            collection,
            whole ? ["Name", "NameEnglish", "FeiId"] : ["Name", "NameEnglish"],
            whole,
            caller,
            search,
            cancellationToken
        );
    }

    /// <param name="whole">Whether the rows are told whole, or by their name and their name in English alone.</param>
    async Task<IReadOnlyList<RegistryMatch>> SearchAsync(
        string read,
        string collection,
        string[] fields,
        bool whole,
        Caller caller,
        NameSearch search,
        CancellationToken cancellationToken
    )
    {
        Admit(read, caller, search);
        var projection = new BsonDocument
        {
            { STANDARD_ID, 1 },
            { "Name", 1 },
            { "NameEnglish", 1 },
        };
        if (whole)
        {
            projection.AddRange(
                new BsonDocument
                {
                    { TenantOwned.TENANT_ID, 1 },
                    { "FeiId", 1 },
                    { "Club.Name", 1 },
                    { "Country.Name", 1 },
                }
            );
        }

        var rows = await Raw(collection)
            .Find(search.Matching(fields))
            .Project(projection)
            .Sort(search.OrderBy("Name", STANDARD_ID))
            .Skip(search.Skip)
            .Limit(search.Take)
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Where(x => x.TryGetValue(STANDARD_ID, out var id) && id.IsBsonBinaryData)
                .Select(x =>
                    whole
                        ? new RegistryMatch(
                            x[STANDARD_ID].AsGuid,
                            TextOf(x, TenantOwned.TENANT_ID) ?? Tenant.LEGACY_ID,
                            TextOf(x, "Name") ?? string.Empty,
                            TextOf(x, "NameEnglish"),
                            TextOf(x, "FeiId"),
                            NestedTextOf(x, "Club", "Name"),
                            NestedTextOf(x, "Country", "Name"),
                            null
                        )
                        : RegistryMatch.NameOnly(
                            x[STANDARD_ID].AsGuid,
                            TextOf(x, "Name") ?? string.Empty,
                            TextOf(x, "NameEnglish")
                        )
                ),
        ];
    }

    /// <summary>
    /// The second guards of a search: the policy has to allow it, and its words have to be enough. A search for less than a
    /// word of a few characters would be a list, and a list is what the registries are not.
    /// </summary>
    static void Admit(string read, Caller caller, NameSearch search)
    {
        Allow(read, caller);
        search.EnsureEnough();
    }

    /// <summary>The second guard of a read of what the registries hold: the policy has to allow it to the caller, which it does for any account.</summary>
    static void Allow(string read, Caller caller)
    {
        var verdict = AccessPolicy.Decide(Capability.ReadRegistryAcrossTenants, caller, AccessScope.Platform);
        if (!verdict.IsAllowed)
        {
            throw new CrossTenantRefusedException(read, verdict.Reason!.Value);
        }
    }

    /// <summary>The documents of a collection, found by the member that holds the id of their Event, but for the ones flagged as deleted.</summary>
    async Task<IReadOnlyList<T>> ReadOfEventsAsync<T>(
        string collection,
        string member,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken
    )
    {
        return ids.Count == 0
            ? []
            : await _database
                .GetCollection<T>(collection)
                .Find(Undeleted(new BsonDocument(member, InAny(ids))))
                .ToListAsync(cancellationToken);
    }

    /// <summary>The documents that the Functions host did not delete, which it did by flagging them: a member it never wrote is not a flag.</summary>
    static BsonDocument Undeleted(BsonDocument conditions)
    {
        conditions["IsDeleted"] = new BsonDocument("$ne", true);
        return conditions;
    }

    static BsonDocument InAny(IEnumerable<Guid> ids)
    {
        return new BsonDocument("$in", new BsonArray(ids.Select(x => (BsonValue)BsonGuids.Binary(x))));
    }

    static string RegistryOf(ParticipantKind kind)
    {
        return kind == ParticipantKind.Athlete ? "athletes" : "horses";
    }

    static BsonDocument ParticipantFields()
    {
        return new BsonDocument
        {
            { STANDARD_ID, 1 },
            { "Name", 1 },
            { "NameEnglish", 1 },
        };
    }

    /// <summary>The names of a row of a registry, none for a document that has no id of the shape the application writes or no name.</summary>
    static Participant? ParticipantOf(ParticipantKind kind, BsonDocument row)
    {
        return BsonGuids.Of(row, STANDARD_ID) is { } id && TextOf(row, "Name") is { } name
            ? new Participant(kind, id, name, TextOf(row, "NameEnglish"))
            : null;
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
