using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Api.Features.Account;
using NoTiming.Api.Features.Tenancy;
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
    /// Makes an Event in the Tenant: a Setup with a name and a location, the country of the Tenant, nothing configured yet,
    /// and the account that makes it as its Main Operator. The Setup is the one the Functions API reads, so the Event shows
    /// in Judge until the Console replaces it.
    /// </summary>
    public async Task<Guid> CreateAsync(
        string tenantId,
        Guid mainOperator,
        string name,
        string location,
        string? feiShowId,
        CancellationToken cancellationToken
    )
    {
        var country =
            (await _countries.AllAsync(cancellationToken)).FirstOrDefault(x => Tenant.ForCountry(x).Id == tenantId)
            ?? throw new TenantCountryMissingException(tenantId);
        var id = Guid.NewGuid();
        var document = new BsonDocument
        {
            { "_id", BsonGuids.Binary(id) },
            { "Name", name },
            { "Location", location },
            { "Country", CountryDocument(country) },
            { "MainOperatorId", BsonGuids.Binary(mainOperator) },
            { "Competitions", new BsonArray() },
            { "Officials", new BsonArray() },
            { "Operators", new BsonArray() },
            { "Loops", new BsonArray() },
            { "Combinations", new BsonArray() },
        };
        if (feiShowId != null)
        {
            document["FeiShowId"] = feiShowId;
        }

        await _tenants.Of(TenantOwned.CONFIGURE_EVENTS, tenantId).InsertAsync(document, cancellationToken);
        return id;
    }

    /// <summary>
    /// Puts the account in as the Main Operator of an Event that has not started, in its Setup. False when the Event is
    /// not there any more.
    /// </summary>
    public Task<bool> AssignMainOperatorAsync(EventRecord record, Guid account, CancellationToken cancellationToken)
    {
        return SetMainOperatorAsync(TenantOwned.CONFIGURE_EVENTS, record, account, cancellationToken);
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

    /// <summary>The country as the Setup embeds it: the fields of a country document, with the constant Tenant of the countries.</summary>
    static BsonDocument CountryDocument(Country country)
    {
        var document = new BsonDocument
        {
            { "_id", BsonGuids.Binary(country.Id) },
            { TenantOwned.TENANT_ID, Tenant.LEGACY_ID },
            { "Name", country.Name },
            { "IsoCode", country.IsoCode },
        };
        if (country.NfCode != null)
        {
            document["NfCode"] = country.NfCode;
        }

        if (country.Locale != null)
        {
            document["Locale"] = country.Locale;
        }

        return document;
    }
}
