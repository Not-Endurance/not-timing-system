using MongoDB.Bson;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// ADR-0009: every Guid is stored, <c>_id</c> included, as a standard-representation BSON UUID. The Participation is written
/// the way the application stores one and the raw BSON is read straight from MongoDB.
/// </summary>
public sealed class GuidStorageTests : IClassFixture<MongoFixture>
{
    readonly StoredDocuments _stored;

    public GuidStorageTests(MongoFixture mongo)
    {
        _stored = new StoredDocuments(mongo.ConnectionString);
    }

    [Fact]
    public async Task A_Guid_id_and_a_Guid_reference_are_stored_as_standard_UUIDs_and_read_back()
    {
        var eventId = Guid.NewGuid();
        var participationId = Guid.NewGuid();
        var participation = IntegrationPayloadFactory.ActiveParticipation(eventId, 1, participationId);
        await _stored.WriteParticipation(participation);

        var stored = await _stored.Read("event_participations", participationId);

        StoredDocuments.AssertStandardUuid(stored["_id"], participationId);
        StoredDocuments.AssertStandardUuid(stored["EventId"], eventId);
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

        var readBack = await _stored.LoadParticipation(participationId);
        Assert.Equal(participationId, readBack.Id);
        Assert.Equal(eventId, readBack.EventId);
        Assert.Equal(participation.Combination.Id, readBack.Combination.Id);
        Assert.Equal(participation.Combination.Athlete.Id, readBack.Combination.Athlete.Id);
        Assert.Equal(participation.Combination.Horse.Id, readBack.Combination.Horse.Id);
        Assert.Equal(participation.Phases.Single().Id, readBack.Phases.Single().Id);
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
