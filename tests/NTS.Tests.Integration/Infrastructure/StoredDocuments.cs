using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using NoTiming.Api.Features.Reference;
using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// The documents as they are stored in MongoDB, read and written without the models of the application, for the tests
/// that say what is in storage and not what the application makes of it. The documents of the application's models are
/// written and loaded the way the application does, with its mapping of the models, so that a test can say what that
/// mapping stores and loads without a host in between.
/// </summary>
internal sealed class StoredDocuments
{
    /// <summary>The Tenant of the documents made before Tenants had a place in them (ADR-0012): the constant of the data as it was.</summary>
    public const string LEGACY_TENANT = "nts";

    public static void AssertStandardUuid(BsonValue value, Guid expected)
    {
        Assert.Equal(BsonType.Binary, value.BsonType);
        Assert.Equal(BsonBinarySubType.UuidStandard, value.AsBsonBinaryData.SubType);
        Assert.Equal(expected, value.AsBsonBinaryData.ToGuid(GuidRepresentation.Standard));
    }

    readonly string _connectionString;
    readonly MongoClient _client;

    public StoredDocuments(string connectionString)
    {
        _connectionString = connectionString;
        _client = new MongoClient(connectionString);
    }

    public async Task<BsonDocument> Read(string collection, Guid id)
    {
        return await Collection(collection).Find(IdFilter(id)).SingleAsync();
    }

    public async Task<List<BsonDocument>> ReadAll(string collection)
    {
        return await Collection(collection).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
    }

    public async Task Replace(string collection, Guid id, BsonDocument document)
    {
        await Collection(collection).ReplaceOneAsync(IdFilter(id), document);
    }

    public async Task Insert(string collection, BsonDocument document)
    {
        await Collection(collection).InsertOneAsync(document);
    }

    public async Task Empty(string collection)
    {
        await Collection(collection).DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
    }

    public Task WriteParticipation(Participation participation, string tenant = LEGACY_TENANT)
    {
        return EventSeed.ParticipationAsync(_connectionString, tenant, participation);
    }

    /// <summary>Replaces the document of the Participation with the one the application stores for it now.</summary>
    public Task ReplaceParticipation(Participation participation, string tenant = LEGACY_TENANT)
    {
        ApiMongo.Configure();
        var model = ParticipationModel.MapFrom(participation);
        model.TenantId = tenant;
        return Replace("event_participations", participation.Id, model.ToBsonDocument());
    }

    public Task WriteRanking(Ranking ranking, string tenant = LEGACY_TENANT)
    {
        return EventSeed.RankingAsync(_connectionString, tenant, ranking);
    }

    public Task WriteHandout(Handout handout, string tenant = LEGACY_TENANT)
    {
        return EventSeed.HandoutAsync(_connectionString, tenant, handout);
    }

    /// <summary>The Participation as the application loads it from its document.</summary>
    public async Task<Participation> LoadParticipation(Guid id)
    {
        ApiMongo.Configure();
        return BsonSerializer.Deserialize<ParticipationModel>(await Read("event_participations", id)).MapToEntity();
    }

    /// <summary>The Participations of the Event as the application loads them.</summary>
    public async Task<IReadOnlyList<Participation>> LoadParticipations(Guid eventId)
    {
        ApiMongo.Configure();
        return
        [
            .. (await OfTheEvent("event_participations", eventId))
                .Select(Deserialize<ParticipationModel>)
                .Select(x => x.MapToEntity()),
        ];
    }

    /// <summary>The Rankings of the Event as the application loads them.</summary>
    public async Task<IReadOnlyList<Ranking>> LoadRankings(Guid eventId)
    {
        ApiMongo.Configure();
        return
        [
            .. (await OfTheEvent("event_rankings", eventId))
                .Select(Deserialize<RankingModel>)
                .Select(x => x.MapToEntity()),
        ];
    }

    /// <summary>The Handouts of the Event as the application loads them.</summary>
    public async Task<IReadOnlyList<Handout>> LoadHandouts(Guid eventId)
    {
        ApiMongo.Configure();
        return
        [
            .. (await OfTheEvent("event_handouts", eventId))
                .Select(Deserialize<HandoutModel>)
                .Select(x => x.MapToEntity()),
        ];
    }

    static T Deserialize<T>(BsonDocument document)
    {
        return BsonSerializer.Deserialize<T>(document);
    }

    Task<List<BsonDocument>> OfTheEvent(string collection, Guid eventId)
    {
        return Collection(collection).Find(new BsonDocument("EventId", EventSeed.Binary(eventId))).ToListAsync();
    }

    IMongoCollection<BsonDocument> Collection(string collection)
    {
        return _client.GetDatabase(UserSeed.DATABASE).GetCollection<BsonDocument>(collection);
    }

    static BsonDocument IdFilter(Guid id)
    {
        return new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard));
    }
}
