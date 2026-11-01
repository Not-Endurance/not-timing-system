using MongoDB.Bson;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Tests.Integration.Harness.Drivers;
using NTS.Tests.Integration.Harness;

namespace NTS.Tests.Integration;

/// <summary>
/// ADR-0006: a stored Ranking holds, per entry, the id of a Participation, a not-ranked mark and the stored rank, and a
/// stored Handout holds its own id, its Event's id and the id of its Participation. Neither holds a Participation. The
/// documents are written and loaded the way the application does and the raw BSON is read straight from MongoDB.
/// </summary>
public sealed class RankingAndHandoutStorageTests : IClassFixture<MongoFixture>
{
    /// <summary>A default (a mark that is false, a rank that is null) is not written: see NtsMongoSerialization.</summary>
    static readonly string[] ENTRY_FIELDS = ["ParticipationId", "IsNotRanked", "Rank"];

    readonly StoredDocuments _stored;

    public RankingAndHandoutStorageTests(MongoFixture mongo)
    {
        _stored = new StoredDocuments(mongo.ConnectionString);
    }

    [Fact]
    public async Task A_stored_Ranking_holds_the_ids_of_its_Participations_and_no_Participation()
    {
        var eventId = Guid.NewGuid();
        var rankingId = Guid.NewGuid();
        var first = IntegrationPayloadFactory.ActiveParticipation(eventId, 1, Guid.NewGuid());
        var second = IntegrationPayloadFactory.ActiveParticipation(eventId, 2, Guid.NewGuid());
        var ranking = CreateRanking(
            eventId,
            rankingId,
            "CEI 1*",
            new RankingEntry(first.Id, false, 1),
            new RankingEntry(second.Id, true)
        );
        await _stored.WriteRanking(ranking);

        var stored = await _stored.Read("event_rankings", rankingId);

        Assert.Equal("CEI 1*", stored["Name"].AsString);
        Assert.Equal("FEI", stored["Ruleset"].AsString);
        Assert.Equal("Senior", stored["Category"].AsString);
        var entries = stored["Entries"].AsBsonArray;
        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry => Assert.Empty(entry.AsBsonDocument.Names.Except(ENTRY_FIELDS)));
        StoredDocuments.AssertStandardUuid(entries[0]["ParticipationId"], first.Id);
        Assert.False(entries[0].AsBsonDocument.GetValue("IsNotRanked", false).AsBoolean);
        Assert.Equal(1, entries[0]["Rank"].AsInt32);
        StoredDocuments.AssertStandardUuid(entries[1]["ParticipationId"], second.Id);
        Assert.True(entries[1]["IsNotRanked"].AsBoolean);
        Assert.True(entries[1].AsBsonDocument.GetValue("Rank", BsonNull.Value).IsBsonNull);

        var readBack = Assert.Single(await _stored.LoadRankings(eventId));
        Assert.Equal(ranking.Entries, readBack.Entries);
    }

    [Fact]
    public async Task A_stored_Handout_holds_its_Event_and_its_Participation_by_id_and_nothing_of_the_Participation()
    {
        var eventId = Guid.NewGuid();
        var handoutId = Guid.NewGuid();
        var participation = IntegrationPayloadFactory.ActiveParticipation(eventId, 1, Guid.NewGuid());
        await _stored.WriteHandout(IntegrationPayloadFactory.Handout(participation, handoutId));

        var stored = await _stored.Read("event_handouts", handoutId);

        StoredDocuments.AssertStandardUuid(stored["_id"], handoutId);
        StoredDocuments.AssertStandardUuid(stored["EventId"], eventId);
        StoredDocuments.AssertStandardUuid(stored["ParticipationId"], participation.Id);
        Assert.DoesNotContain("Participation", stored.Names);
        var readBack = Assert.Single(await _stored.LoadHandouts(eventId));
        Assert.Equal(handoutId, readBack.Id);
        Assert.Equal(eventId, readBack.EventId);
        Assert.Equal(participation.Id, readBack.ParticipationId);
    }

    static Ranking CreateRanking(Guid eventId, Guid id, string name, params RankingEntry[] entries)
    {
        return new Ranking(
            name,
            CompetitionRuleset.FEI,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            null,
            null,
            entries,
            eventId,
            id
        );
    }
}
