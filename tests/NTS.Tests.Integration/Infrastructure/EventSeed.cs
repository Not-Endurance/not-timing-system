using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Api.Features.Reference;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Core.Aggregates;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// Events as the Api and the Functions API keep them, written straight into the collections: a Setup in
/// <c>configure_events</c> and, once an Event has started, its Core document in <c>event_informations</c> with the Tenant,
/// the Main Operator and the end of its last day (ADR-0007, ADR-0012). A test makes an Event Live or Historic by giving it
/// an end after or before the clock of the host.
/// </summary>
internal static class EventSeed
{
    /// <summary>The collections whose documents belong to an Event and to its Tenant.</summary>
    public static readonly string[] EVENT_COLLECTIONS =
    [
        "event_officials",
        "event_operators",
        "event_participations",
        "event_rankings",
        "event_handouts",
    ];

    public static IMongoCollection<BsonDocument> Setups(string mongoConnectionString)
    {
        return Collection(mongoConnectionString, "configure_events");
    }

    public static IMongoCollection<BsonDocument> Cores(string mongoConnectionString)
    {
        return Collection(mongoConnectionString, "event_informations");
    }

    public static IMongoCollection<BsonDocument> Grants(string mongoConnectionString)
    {
        return Collection(mongoConnectionString, "event_grants");
    }

    public static BsonBinaryData Binary(Guid id)
    {
        return new BsonBinaryData(id, GuidRepresentation.Standard);
    }

    /// <summary>An Event that has not started: its Setup only.</summary>
    public static async Task<Guid> SetupAsync(
        string mongoConnectionString,
        string tenant,
        Guid? mainOperator,
        string? name = null,
        Guid? id = null
    )
    {
        id ??= Guid.NewGuid();
        var document = new BsonDocument
        {
            { "_id", Binary(id.Value) },
            { "TenantId", tenant },
            { "Name", name ?? "Event " + id.Value.ToString("N")[..6] },
            { "Location", "Sofia" },
            { "Country", RegistrySeed.CountryOf("Bulgaria", "BG") },
            { "Competitions", new BsonArray() },
            { "Officials", new BsonArray() },
            { "Operators", new BsonArray() },
            { "Loops", new BsonArray() },
            { "Combinations", new BsonArray() },
        };
        if (mainOperator != null)
        {
            document["MainOperatorId"] = Binary(mainOperator.Value);
        }

        await Setups(mongoConnectionString).InsertOneAsync(document);
        return id.Value;
    }

    /// <summary>
    /// An Event that has not started with a Setup that can: a competition of three phases with a combination in it, an
    /// Official and an Operator, and the FEI configuration complete (<see cref="SetupFactory.Full"/>).
    /// </summary>
    public static async Task<Guid> FullSetupAsync(
        string mongoConnectionString,
        string tenant,
        Guid? mainOperator,
        string name = "Full Setup"
    )
    {
        ApiMongo.Configure();
        var id = Guid.NewGuid();
        var model = ConfigureEventModel.From(SetupFactory.Full(id, name));
        model.TenantId = tenant;
        model.MainOperatorId = mainOperator;
        await Setups(mongoConnectionString).InsertOneAsync(model.ToBsonDocument());
        return id;
    }

    /// <summary>How many documents of the collection an Event has: what it made when it started.</summary>
    public static async Task<long> CountOfAsync(string mongoConnectionString, string collection, Guid eventId)
    {
        return await Collection(mongoConnectionString, collection)
            .CountDocumentsAsync(new BsonDocument("EventId", Binary(eventId)));
    }

    /// <summary>
    /// One document of everything an Event keeps beside its Core document, and the state of a person, so that a test can
    /// tell that all of it goes when the Event is reset.
    /// </summary>
    public static async Task KeepsAsync(string mongoConnectionString, string tenant, Guid eventId)
    {
        foreach (var collection in EVENT_COLLECTIONS)
        {
            await Collection(mongoConnectionString, collection)
                .InsertOneAsync(
                    new BsonDocument
                    {
                        { "_id", Binary(Guid.NewGuid()) },
                        { "TenantId", tenant },
                        { "EventId", Binary(eventId) },
                    }
                );
        }

        await Collection(mongoConnectionString, "event_user_sessions")
            .InsertOneAsync(new BsonDocument { { "_id", Binary(Guid.NewGuid()) }, { "EventId", Binary(eventId) } });
    }

    /// <summary>An Event that has started: the Core document of it, which ends at the instant given.</summary>
    public static async Task StartAsync(
        string mongoConnectionString,
        Guid id,
        string tenant,
        Guid? mainOperator,
        DateTimeOffset end
    )
    {
        var document = new BsonDocument
        {
            { "_id", Binary(id) },
            { "TenantId", tenant },
            { "Name", "Event " + id.ToString("N")[..6] },
            { "Location", "Sofia" },
            { "Country", RegistrySeed.CountryOf("Bulgaria", "BG") },
            { "StartDay", new BsonDateTime(end.AddDays(-1).UtcDateTime) },
            { "EndDay", new BsonDateTime(end.UtcDateTime) },
        };
        if (mainOperator != null)
        {
            document["MainOperatorId"] = Binary(mainOperator.Value);
        }

        await Cores(mongoConnectionString).InsertOneAsync(document);
    }

    /// <summary>An Event that has started and is Live for another day after the instant given as now.</summary>
    public static async Task<Guid> LiveAsync(
        string mongoConnectionString,
        string tenant,
        Guid? mainOperator,
        DateTimeOffset now
    )
    {
        var id = await SetupAsync(mongoConnectionString, tenant, mainOperator);
        await StartAsync(mongoConnectionString, id, tenant, mainOperator, now.AddDays(1));
        return id;
    }

    /// <summary>An Event that has started and ended a day before the instant given as now.</summary>
    public static async Task<Guid> HistoricAsync(
        string mongoConnectionString,
        string tenant,
        Guid? mainOperator,
        DateTimeOffset now
    )
    {
        var id = await SetupAsync(mongoConnectionString, tenant, mainOperator);
        await StartAsync(mongoConnectionString, id, tenant, mainOperator, now.AddDays(-1));
        return id;
    }

    /// <summary>A grant as the Api keeps it, written straight into the collection, so that it can be given to any Event at any stage.</summary>
    public static async Task<Guid> GrantAsync(
        string mongoConnectionString,
        string tenant,
        Guid eventId,
        string kind,
        string? officialRole,
        string email,
        Guid? accountId
    )
    {
        var id = Guid.NewGuid();
        var document = new BsonDocument
        {
            { "_id", Binary(id) },
            { "TenantId", tenant },
            { "EventId", Binary(eventId) },
            { "Kind", kind },
            { "Email", email },
        };
        if (officialRole != null)
        {
            document["OfficialRole"] = officialRole;
        }

        if (accountId != null)
        {
            document["AccountId"] = Binary(accountId.Value);
        }

        await Grants(mongoConnectionString).InsertOneAsync(document);
        return id;
    }

    /// <summary>
    /// A Participation as the Api keeps it: the model of the Participation stamped with the Tenant of its Event, and the
    /// version its document carries (a document that was never written since the versions came has none, which is 0).
    /// </summary>
    public static async Task ParticipationAsync(
        string mongoConnectionString,
        string tenant,
        Participation participation,
        int version = 0
    )
    {
        ApiMongo.Configure();
        var model = NTS.Contracts.Core.Models.ParticipationModel.MapFrom(participation);
        model.TenantId = tenant;
        model.Version = version;
        await Collection(mongoConnectionString, "event_participations").InsertOneAsync(model.ToBsonDocument());
    }

    public static async Task RankingAsync(string mongoConnectionString, string tenant, Ranking ranking)
    {
        ApiMongo.Configure();
        var model = NTS.Contracts.Core.Models.RankingModel.From(ranking);
        model.TenantId = tenant;
        await Collection(mongoConnectionString, "event_rankings").InsertOneAsync(model.ToBsonDocument());
    }

    public static async Task OfficialAsync(string mongoConnectionString, string tenant, Official official)
    {
        ApiMongo.Configure();
        var model = NTS.Contracts.Core.Models.OfficialModel.MapFrom(official);
        model.TenantId = tenant;
        await Collection(mongoConnectionString, "event_officials").InsertOneAsync(model.ToBsonDocument());
    }

    public static async Task HandoutAsync(string mongoConnectionString, string tenant, Handout handout)
    {
        ApiMongo.Configure();
        var model = NTS.Contracts.Core.Models.HandoutModel.From(handout);
        model.TenantId = tenant;
        await Collection(mongoConnectionString, "event_handouts").InsertOneAsync(model.ToBsonDocument());
    }

    public static async Task<BsonDocument?> SetupOfAsync(string mongoConnectionString, Guid id)
    {
        return await Setups(mongoConnectionString).Find(new BsonDocument("_id", Binary(id))).FirstOrDefaultAsync();
    }

    public static async Task<BsonDocument?> CoreOfAsync(string mongoConnectionString, Guid id)
    {
        return await Cores(mongoConnectionString).Find(new BsonDocument("_id", Binary(id))).FirstOrDefaultAsync();
    }

    static IMongoCollection<BsonDocument> Collection(string mongoConnectionString, string name)
    {
        return new MongoClient(mongoConnectionString).GetDatabase(UserSeed.DATABASE).GetCollection<BsonDocument>(name);
    }
}
