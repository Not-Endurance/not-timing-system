using MongoDB.Bson;
using MongoDB.Driver;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// Events as the Api and the Functions API keep them, written straight into the collections: a Setup in
/// <c>configure_events</c> and, once an Event has started, its Core document in <c>event_informations</c> with the Tenant,
/// the Main Operator and the end of its last day (ADR-0007, ADR-0012). A test makes an Event Live or Historic by giving it
/// an end after or before the clock of the host.
/// </summary>
internal static class EventSeed
{
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
        string? name = null
    )
    {
        var id = Guid.NewGuid();
        var document = new BsonDocument
        {
            { "_id", Binary(id) },
            { "TenantId", tenant },
            { "Name", name ?? "Event " + id.ToString("N")[..6] },
            { "Location", "Sofia" },
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
        return id;
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
            { "StartDay", new BsonDateTime(end.AddDays(-1).UtcDateTime) },
            { "EndDay", new BsonDateTime(end.UtcDateTime) },
            { "IsActive", true },
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
