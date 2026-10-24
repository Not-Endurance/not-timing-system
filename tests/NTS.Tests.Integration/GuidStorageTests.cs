using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Nexus.HTTP.Mongo;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// ADR-0009: every Guid is stored, <c>_id</c> included, as a standard-representation BSON UUID, and a filter on a Guid
/// foreign key finds the right documents. The documents go in through the Functions API and the raw BSON is read
/// straight from MongoDB.
/// </summary>
public sealed class GuidStorageTests : IClassFixture<NtsIntegrationFixture>
{
    readonly NtsIntegrationFixture _fixture;

    public GuidStorageTests(NtsIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task A_Guid_id_and_a_Guid_reference_are_stored_as_standard_UUIDs_and_read_back()
    {
        using var nexus = new NexusApiDriver(_fixture.NexusBaseUrl);
        var eventId = Guid.NewGuid();
        var participationId = Guid.NewGuid();
        await nexus.Create(IntegrationPayloadFactory.EventInformation(eventId));
        var participation = IntegrationPayloadFactory.ActiveParticipation(eventId, 1, participationId);
        await nexus.Create(participation);

        var stored = await ReadStored(MongoConstants.PARTICIPATIONS_COLLECTION, participationId);

        AssertStandardUuid(stored["_id"], participationId);
        AssertStandardUuid(stored["EventId"], eventId);
        var uuids = Walk("", stored).Where(x => x.Value.BsonType == BsonType.Binary).ToList();
        Assert.True(
            uuids.Count > 2,
            "The embedded ids should be UUIDs too: " + string.Join(", ", uuids.Select(x => x.Path))
        );
        Assert.All(
            uuids,
            x =>
                Assert.True(
                    x.Value.AsBsonBinaryData.SubType == BsonBinarySubType.UuidStandard,
                    $"{x.Path} is stored as {x.Value.AsBsonBinaryData.SubType}"
                )
        );
        Assert.Empty(
            Walk("", stored)
                .Where(x => x.Value.BsonType is BsonType.Int32 or BsonType.Int64)
                .Where(x => IsOwnIdentifier(x.Path))
                .Select(x => x.Path)
        );

        var readBack = await nexus.ReadParticipation(eventId, participationId);
        Assert.Equal(participationId, readBack.Id);
        Assert.Equal(eventId, readBack.EventId);
        Assert.Equal(participation.Combination.Id, readBack.Combination.Id);
        Assert.Equal(participation.Combination.Athlete.Id, readBack.Combination.Athlete.Id);
        Assert.Equal(participation.Combination.Horse.Id, readBack.Combination.Horse.Id);
        Assert.Equal(participation.Phases.Single().Id, readBack.Phases.Single().Id);
    }

    [Fact]
    public async Task A_filter_on_a_Guid_foreign_key_returns_the_documents_of_that_Event_only()
    {
        using var nexus = new NexusApiDriver(_fixture.NexusBaseUrl);
        var eventId = Guid.NewGuid();
        var otherEventId = Guid.NewGuid();
        var participationId = Guid.NewGuid();
        var otherParticipationId = Guid.NewGuid();
        await nexus.Create(IntegrationPayloadFactory.EventInformation(eventId));
        await nexus.Create(IntegrationPayloadFactory.EventInformation(otherEventId));
        await nexus.Create(IntegrationPayloadFactory.ActiveParticipation(eventId, 1, participationId));
        await nexus.Create(IntegrationPayloadFactory.ActiveParticipation(otherEventId, 2, otherParticipationId));

        Assert.Equal(participationId, Assert.Single(await nexus.ReadParticipations(eventId)).Id);
        Assert.Equal(otherParticipationId, Assert.Single(await nexus.ReadParticipations(otherEventId)).Id);
        Assert.Empty(await nexus.ReadParticipations(Guid.NewGuid()));
    }

    async Task<BsonDocument> ReadStored(string collection, Guid id)
    {
        var documents = new MongoClient(_fixture.MongoConnectionString)
            .GetDatabase(MongoConstants.NTS_DATABASE)
            .GetCollection<BsonDocument>(collection);
        return await documents
            .Find(new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard)))
            .SingleAsync();
    }

    static void AssertStandardUuid(BsonValue value, Guid expected)
    {
        Assert.Equal(BsonType.Binary, value.BsonType);
        Assert.Equal(BsonBinarySubType.UuidStandard, value.AsBsonBinaryData.SubType);
        Assert.Equal(expected, value.AsBsonBinaryData.ToGuid(GuidRepresentation.Standard));
    }

    /// <summary>An identifier of ours: <c>_id</c> or <c>*Id</c>. The FEI identifiers belong to the federation.</summary>
    static bool IsOwnIdentifier(string path)
    {
        var name = path[(path.LastIndexOf('.') + 1)..];
        return !name.StartsWith("Fei", StringComparison.Ordinal)
            && (name == "_id" || name.EndsWith("Id", StringComparison.Ordinal));
    }

    static IEnumerable<(string Path, BsonValue Value)> Walk(string path, BsonValue value)
    {
        switch (value)
        {
            case BsonDocument document:
                foreach (var element in document)
                {
                    foreach (var nested in Walk($"{path}.{element.Name}", element.Value))
                    {
                        yield return nested;
                    }
                }
                break;
            case BsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    foreach (var nested in Walk($"{path}[{index}]", array[index]))
                    {
                        yield return nested;
                    }
                }
                break;
            default:
                yield return (path, value);
                break;
        }
    }
}
