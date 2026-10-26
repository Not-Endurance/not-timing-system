using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Api.Features.Account;
using NoTiming.Api.Features.Tenancy;
using NTS.Domain.Access;
using NTS.Domain.Aggregates;

namespace NoTiming.Api.Features.Events;

/// <summary>An Event with what has to be known about it to say who may do what: where it belongs and its stage now.</summary>
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

    /// <summary>The state of the place the policy is asked about the Event in.</summary>
    public PlaceState State => PlaceState.OfEvent(Stage);
}

/// <summary>The country of a Tenant is not among the countries, so there is nothing to make its Events in.</summary>
internal sealed class TenantCountryMissingException : InvalidOperationException
{
    public TenantCountryMissingException(string tenantId)
        : base($"The country of Tenant '{tenantId}' is not among the countries.") { }
}

/// <summary>
/// Making an Event, and where it belongs (ADR-0012, #643): an Event is made in a Tenant by Root and has no Console Operator
/// until Root assigns one, as a grant. Where an Event belongs is a field of its document, the Setup's before it starts and
/// the Core's once it has, and it travels with it when it starts. The writes go through the Tenant's collection, so they
/// cannot reach an Event of another Tenant; the reads of an Event by its id are the named capability of
/// <see cref="CrossTenantReads"/>.
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
    /// Makes an Event in the Tenant: a Setup with a name and a location, the country of the Tenant and nothing configured
    /// yet, with the id the client made for it or one of the server's. Created is false when an Event with the id is there
    /// already, whoever's it is.
    /// </summary>
    public async Task<(Guid Id, bool Created)> CreateAsync(
        string tenantId,
        Guid? requestedId,
        string name,
        string location,
        string? feiShowId,
        CancellationToken cancellationToken
    )
    {
        var country =
            (await _countries.AllAsync(cancellationToken)).FirstOrDefault(x => Tenant.ForCountry(x).Id == tenantId)
            ?? throw new TenantCountryMissingException(tenantId);
        var id = requestedId ?? Guid.NewGuid();
        var document = new BsonDocument
        {
            { "_id", BsonGuids.Binary(id) },
            { "Name", name },
            { "Location", location },
            { "Country", CountryDocument(country) },
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

        try
        {
            await _tenants.Of(TenantOwned.CONFIGURE_EVENTS, tenantId).InsertAsync(document, cancellationToken);
            return (id, true);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return (id, false);
        }
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
