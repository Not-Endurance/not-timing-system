using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Api.Features.Reference;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Core.Aggregates;

namespace NTS.Tests.Integration.Harness;

/// <summary>
/// Events as the Api keeps them, written straight into the collections: a Setup in
/// <c>configure_events</c> and, once an Event has started, its Core document in <c>event_informations</c> with the Tenant
/// and the end of its last day (ADR-0007, ADR-0012). The Console Operators of an Event are grants, so an account that is to
/// run an Event is given one in <c>event_grants</c> (#671). A test makes an Event Live or Historic by giving it an end after
/// or before the clock of the host.
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

    /// <summary>An Event that has not started: its Setup only, and the account named is a Console Operator of it.</summary>
    public static async Task<Guid> SetupAsync(
        string mongoConnectionString,
        string tenant,
        Guid? consoleOperator,
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
        await Setups(mongoConnectionString).InsertOneAsync(document);
        await ConsoleOperatorAsync(mongoConnectionString, tenant, id.Value, consoleOperator);
        return id.Value;
    }

    /// <summary>
    /// The account is made a Console Operator of the Event, with the email it has (an account that is not there is given one of
    /// its own), once: asking again for an account that is one already does nothing. Nobody is none.
    /// </summary>
    public static async Task ConsoleOperatorAsync(
        string mongoConnectionString,
        string tenant,
        Guid eventId,
        Guid? account
    )
    {
        if (account == null)
        {
            return;
        }

        var held = new BsonDocument
        {
            { "EventId", Binary(eventId) },
            { "Kind", "ConsoleOperator" },
            { "AccountId", Binary(account.Value) },
        };
        if (await Grants(mongoConnectionString).CountDocumentsAsync(held) > 0)
        {
            return;
        }

        var user = await UserSeed
            .Users(mongoConnectionString)
            .Find(new BsonDocument("_id", Binary(account.Value)))
            .FirstOrDefaultAsync();
        var email = user?.GetValue("Email", BsonNull.Value) is { IsString: true } stored
            ? stored.AsString
            : $"{account.Value:N}@console-operator.test";
        await GrantAsync(mongoConnectionString, tenant, eventId, "ConsoleOperator", null, email, account);
    }

    /// <summary>
    /// An Event that has not started with a Setup that can: a competition of three phases with a combination in it, an
    /// Official and an Operator, and the FEI configuration complete (<see cref="SetupFactory.Full"/>).
    /// </summary>
    public static async Task<Guid> FullSetupAsync(
        string mongoConnectionString,
        string tenant,
        Guid? consoleOperator,
        string name = "Full Setup",
        bool startNumberInTwoCompetitions = false,
        bool eachHasAnother = true
    )
    {
        ApiMongo.Configure();
        var id = Guid.NewGuid();
        var model = ConfigureEventModel.From(
            SetupFactory.Full(
                id,
                name,
                startNumberInTwoCompetitions: startNumberInTwoCompetitions,
                eachHasAnother: eachHasAnother
            )
        );
        model.TenantId = tenant;
        await Setups(mongoConnectionString).InsertOneAsync(model.ToBsonDocument());
        await ConsoleOperatorAsync(mongoConnectionString, tenant, id, consoleOperator);
        return id;
    }

    /// <summary>
    /// The Setup of an Event that exists, made whole: what a person configures after making an Event, which a test of the whole way
    /// has no need to do by hand. Its competition starts at the time given (<see cref="SetupFactory.Full"/>).
    /// </summary>
    public static async Task MakeWholeAsync(
        string mongoConnectionString,
        string tenant,
        Guid id,
        string name,
        DateTimeOffset start
    )
    {
        ApiMongo.Configure();
        var model = ConfigureEventModel.From(SetupFactory.Full(id, name, start));
        model.TenantId = tenant;
        await Setups(mongoConnectionString)
            .ReplaceOneAsync(new BsonDocument("_id", Binary(id)), model.ToBsonDocument());
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

    /// <summary>An Event that has started: the Core document of it, which ends at the instant given, and the account named is a Console Operator of it.</summary>
    public static async Task StartAsync(
        string mongoConnectionString,
        Guid id,
        string tenant,
        Guid? consoleOperator,
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
        await Cores(mongoConnectionString).InsertOneAsync(document);
        await ConsoleOperatorAsync(mongoConnectionString, tenant, id, consoleOperator);
    }

    /// <summary>An Event that has started and is Live for another day after the instant given as now.</summary>
    public static async Task<Guid> LiveAsync(
        string mongoConnectionString,
        string tenant,
        Guid? consoleOperator,
        DateTimeOffset now
    )
    {
        var id = await SetupAsync(mongoConnectionString, tenant, consoleOperator);
        await StartAsync(mongoConnectionString, id, tenant, consoleOperator, now.AddDays(1));
        return id;
    }

    /// <summary>An Event that has started and ended a day before the instant given as now.</summary>
    public static async Task<Guid> HistoricAsync(
        string mongoConnectionString,
        string tenant,
        Guid? consoleOperator,
        DateTimeOffset now
    )
    {
        var id = await SetupAsync(mongoConnectionString, tenant, consoleOperator);
        await StartAsync(mongoConnectionString, id, tenant, consoleOperator, now.AddDays(-1));
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
        Guid? accountId,
        Guid? id = null
    )
    {
        var grantId = id ?? Guid.NewGuid();
        var document = new BsonDocument
        {
            { "_id", Binary(grantId) },
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
        return grantId;
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
