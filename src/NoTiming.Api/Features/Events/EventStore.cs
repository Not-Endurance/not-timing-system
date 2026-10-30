using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Api.Features.Account;
using NoTiming.Api.Features.Tenancy;
using NTS.Contracts.Shared.Models;
using NTS.Domain.Access;
using NTS.Domain.Aggregates;

namespace NoTiming.Api.Features.Events;

/// <summary>An Event with what has to be known about it to say who may do what: where it belongs, who runs it, and its stage now.</summary>
internal sealed class EventFacts
{
    public EventFacts(EventRecord record, EventStage stage)
    {
        Record = record;
        Stage = stage;
    }

    public EventRecord Record { get; }
    public EventStage Stage { get; }
    public Guid Id => Record.Id;
    public string TenantId => Record.TenantId;

    /// <summary>The place the policy is asked about the Event in: it knows what the caller has been granted on it.</summary>
    public AccessScope ScopeFor(CallerGrants grants)
    {
        return AccessScope.ForEvent(Record.TenantId, Record.MainOperatorId, Stage, grants);
    }
}

/// <summary>The country of a Tenant is not among the countries, so there is nothing to make its Events in.</summary>
internal sealed class TenantCountryMissingException : InvalidOperationException
{
    public TenantCountryMissingException(string tenantId)
        : base($"The country of Tenant '{tenantId}' is not among the countries.") { }
}

/// <summary>
/// The authority over an Event (ADR-0012, #643): an Event is made in a Tenant by a Tenant Root, who is its Main Operator
/// until it assigns another account while the Event has not started, and the Main Operator hands it over once it is Live.
/// Where an Event belongs and who runs it are fields of the document of the Event: the Setup's before it starts and the
/// Core's once it has, as a hand-over leaves it. The writes go through the Tenant's collection, so they cannot reach an
/// Event of another Tenant; the reads of an Event by its id are the named capability of <see cref="CrossTenantReads"/>.
/// </summary>
internal sealed class EventStore
{
    readonly CrossTenantReads _reads;
    readonly TenantCollections _tenants;
    readonly SelectableCountries _countries;
    readonly TimeProvider _time;

    public EventStore(
        CrossTenantReads reads,
        TenantCollections tenants,
        SelectableCountries countries,
        TimeProvider time
    )
    {
        _reads = reads;
        _tenants = tenants;
        _countries = countries;
        _time = time;
    }

    public async Task<EventFacts?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var record = await _reads.FindEventAsync(id, cancellationToken);
        return record == null ? null : new EventFacts(record, record.StageAt(_time.GetUtcNow()));
    }

    /// <summary>
    /// The country of the Tenant, which an Event made in it has unless it is made with another. No Event is made in a
    /// Tenant whose country is not among the countries.
    /// </summary>
    public async Task<CountryModel> CountryOfAsync(string tenantId, CancellationToken cancellationToken)
    {
        var country =
            (await _countries.AllAsync(cancellationToken)).FirstOrDefault(x => Tenant.ForCountry(x).Id == tenantId)
            ?? throw new TenantCountryMissingException(tenantId);
        return CountryModel.From(country);
    }

    /// <summary>
    /// Puts the account in as the Main Operator of an Event that has not started, in its Setup. False when the Event is
    /// not there any more.
    /// </summary>
    public Task<bool> AssignMainOperatorAsync(EventRecord record, Guid account, CancellationToken cancellationToken)
    {
        return SetMainOperatorAsync(TenantOwned.CONFIGURE_EVENTS, record, account, cancellationToken);
    }

    /// <summary>
    /// The ids of the Events of the Tenant that the account runs now: the ones it was made the Main Operator of that have
    /// not started, and the started ones it holds. A started Event is the Core's, so what the Setup says of its Main
    /// Operator does not count once it has started, as the reads across Tenants have it.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> RunByAsync(
        string? tenantId,
        Guid account,
        CancellationToken cancellationToken
    )
    {
        var mine = new BsonDocument("MainOperatorId", BsonGuids.Binary(account));
        var cores = _tenants.Of(TenantOwned.EVENT_INFORMATIONS, tenantId);
        var configured = await _tenants
            .Of(TenantOwned.CONFIGURE_EVENTS, tenantId)
            .FindIdsAsync(mine, cancellationToken);
        var held = await cores.FindIdsAsync(mine, cancellationToken);
        var started = configured.Count == 0 ? [] : await cores.FindIdsAsync(IdsOf(configured), cancellationToken);
        return [.. configured.Except(started).Union(held)];
    }

    /// <summary>
    /// The Main Operator that each of the Events has now, for those of the ids that have started: it is the Core's, which a
    /// hand-over changes and the Setup does not know.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, Guid?>> StartedOperatorsAsync(
        string? tenantId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken
    )
    {
        var operators = new Dictionary<Guid, Guid?>();
        if (ids.Count == 0)
        {
            return operators;
        }

        var cores = await _tenants
            .Of(TenantOwned.EVENT_INFORMATIONS, tenantId)
            .FindAsync(IdsOf(ids), cancellationToken);
        foreach (var core in cores)
        {
            if (BsonGuids.Of(core, "_id") is { } id)
            {
                operators[id] = BsonGuids.Of(core, "MainOperatorId");
            }
        }

        return operators;
    }

    /// <summary>Puts the account in as the Main Operator of an Event that is Live, in its Core document.</summary>
    public Task<bool> HandOverAsync(EventRecord record, Guid account, CancellationToken cancellationToken)
    {
        return SetMainOperatorAsync(TenantOwned.EVENT_INFORMATIONS, record, account, cancellationToken);
    }

    Task<bool> SetMainOperatorAsync(
        string collection,
        EventRecord record,
        Guid account,
        CancellationToken cancellationToken
    )
    {
        return _tenants
            .Of(collection, record.TenantId)
            .UpdateAsync(
                new BsonDocument("_id", BsonGuids.Binary(record.Id)),
                Builders<BsonDocument>.Update.Set("MainOperatorId", BsonGuids.Binary(account)),
                cancellationToken
            );
    }

    static BsonDocument IdsOf(IEnumerable<Guid> ids)
    {
        return new BsonDocument("_id", new BsonDocument("$in", new BsonArray(ids.Select(BsonGuids.Binary))));
    }
}
