using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Nexus.HTTP.Mongo;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// The documents as they are stored in MongoDB, read and written without the models of the application, for the tests
/// that say what is in storage and not what the application makes of it.
/// </summary>
internal sealed class StoredDocuments
{
    public static void AssertStandardUuid(BsonValue value, Guid expected)
    {
        Assert.Equal(BsonType.Binary, value.BsonType);
        Assert.Equal(BsonBinarySubType.UuidStandard, value.AsBsonBinaryData.SubType);
        Assert.Equal(expected, value.AsBsonBinaryData.ToGuid(GuidRepresentation.Standard));
    }

    readonly MongoClient _client;

    public StoredDocuments(string connectionString)
    {
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

    IMongoCollection<BsonDocument> Collection(string collection)
    {
        return _client.GetDatabase(MongoConstants.NTS_DATABASE).GetCollection<BsonDocument>(collection);
    }

    static BsonDocument IdFilter(Guid id)
    {
        return new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard));
    }
}
