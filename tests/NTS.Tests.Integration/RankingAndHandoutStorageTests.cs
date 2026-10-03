using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Serialization.JSON;
using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Nexus.HTTP.Mongo;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// ADR-0006: a stored Ranking holds, per entry, the id of a Participation, a not-ranked mark and the stored rank, and a
/// stored Handout holds its own id, its Event's id and the id of its Participation. Neither holds a Participation. The
/// documents go in through the Functions API and the raw BSON is read straight from MongoDB.
/// </summary>
public sealed class RankingAndHandoutStorageTests : IClassFixture<NtsIntegrationFixture>
{
    /// <summary>A default (a mark that is false, a rank that is null) is not written: see NtsMongoSerialization.</summary>
    static readonly string[] ENTRY_FIELDS = ["ParticipationId", "IsNotRanked", "Rank"];

    readonly NtsIntegrationFixture _fixture;

    public RankingAndHandoutStorageTests(NtsIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task A_stored_Ranking_holds_the_ids_of_its_Participations_and_no_Participation()
    {
        using var nexus = new NexusApiDriver(_fixture.NexusBaseUrl);
        var eventId = Guid.NewGuid();
        var rankingId = Guid.NewGuid();
        var first = IntegrationPayloadFactory.ActiveParticipation(eventId, 1, Guid.NewGuid());
        var second = IntegrationPayloadFactory.ActiveParticipation(eventId, 2, Guid.NewGuid());
        await Seed(nexus, eventId, first, second);
        var ranking = CreateRanking(
            eventId,
            rankingId,
            "CEI 1*",
            new RankingEntry(first.Id, false, 1),
            new RankingEntry(second.Id, true)
        );
        await nexus.Create(ranking);

        var stored = await ReadStored(MongoConstants.RANKINGS_COLLECTION, rankingId);

        Assert.Equal("CEI 1*", stored["Name"].AsString);
        Assert.Equal("FEI", stored["Ruleset"].AsString);
        Assert.Equal("Senior", stored["Category"].AsString);
        var entries = stored["Entries"].AsBsonArray;
        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry => Assert.Empty(entry.AsBsonDocument.Names.Except(ENTRY_FIELDS)));
        AssertStandardUuid(entries[0]["ParticipationId"], first.Id);
        Assert.False(entries[0].AsBsonDocument.GetValue("IsNotRanked", false).AsBoolean);
        Assert.Equal(1, entries[0]["Rank"].AsInt32);
        AssertStandardUuid(entries[1]["ParticipationId"], second.Id);
        Assert.True(entries[1]["IsNotRanked"].AsBoolean);
        Assert.True(entries[1].AsBsonDocument.GetValue("Rank", BsonNull.Value).IsBsonNull);

        var readBack = Assert.Single(await nexus.ReadRankings(eventId));
        Assert.Equal(ranking.Entries, readBack.Entries);
    }

    [Fact]
    public async Task A_stored_Handout_holds_its_Event_and_its_Participation_by_id_and_nothing_of_the_Participation()
    {
        using var nexus = new NexusApiDriver(_fixture.NexusBaseUrl);
        var eventId = Guid.NewGuid();
        var handoutId = Guid.NewGuid();
        var participation = IntegrationPayloadFactory.ActiveParticipation(eventId, 1, Guid.NewGuid());
        await Seed(nexus, eventId, participation);
        await nexus.Create(IntegrationPayloadFactory.Handout(participation, handoutId));

        var stored = await ReadStored(MongoConstants.HANDOUTS_COLLECTION, handoutId);

        AssertStandardUuid(stored["_id"], handoutId);
        AssertStandardUuid(stored["EventId"], eventId);
        AssertStandardUuid(stored["ParticipationId"], participation.Id);
        Assert.DoesNotContain("Participation", stored.Names);
        var readBack = Assert.Single(await nexus.ReadHandouts(eventId));
        Assert.Equal(handoutId, readBack.Id);
        Assert.Equal(eventId, readBack.EventId);
        Assert.Equal(participation.Id, readBack.ParticipationId);
    }

    [Fact]
    public async Task The_Handouts_of_a_Participation_are_read_with_a_filter_the_server_applies()
    {
        using var nexus = new NexusApiDriver(_fixture.NexusBaseUrl);
        var eventId = Guid.NewGuid();
        var first = IntegrationPayloadFactory.ActiveParticipation(eventId, 1, Guid.NewGuid());
        var second = IntegrationPayloadFactory.ActiveParticipation(eventId, 2, Guid.NewGuid());
        await Seed(nexus, eventId, first, second);
        var ofFirstOnPhaseOne = IntegrationPayloadFactory.Handout(first, Guid.NewGuid());
        var ofSecond = IntegrationPayloadFactory.Handout(second, Guid.NewGuid());
        var ofFirstOnPhaseTwo = IntegrationPayloadFactory.Handout(first, Guid.NewGuid());
        await nexus.Create(ofFirstOnPhaseOne);
        await nexus.Create(ofSecond);
        await nexus.Create(ofFirstOnPhaseTwo);
        Guid[] expected = [ofFirstOnPhaseOne.Id, ofFirstOnPhaseTwo.Id];

        var asked = ODataApiFilterAdapter.ParseFilters<Handout>([x => x.ParticipationId == first.Id]);
        var theServerFilters = await ReadHandoutsFrom(HttpHelper.AddQueryString("api/handouts", asked));
        var withNoFilter = await ReadHandoutsFrom("api/handouts");
        await using var client = new ClientDriver(_fixture.ApiBaseUrl, _fixture.NexusBaseUrl, null, "reader");
        var theUiAsks = await client
            .GetRequiredService<IRepository<Handout>>()
            .ReadMany(x => x.ParticipationId == first.Id);

        Assert.Equal(expected.Order(), theServerFilters.Select(x => x.Id).Order());
        Assert.Contains(ofSecond.Id, withNoFilter.Select(x => x.Id)); // what the filter left out is there to be found
        Assert.Equal(expected.Order(), theUiAsks.Select(x => x.Id).Order());
    }

    [Fact]
    public async Task A_Ranking_is_not_written_again_when_a_Participation_it_names_changes()
    {
        using var nexus = new NexusApiDriver(_fixture.NexusBaseUrl);
        var eventId = Guid.NewGuid();
        var rankingId = Guid.NewGuid();
        var first = IntegrationPayloadFactory.ActiveParticipation(eventId, 1, Guid.NewGuid());
        var second = IntegrationPayloadFactory.ActiveParticipation(eventId, 2, Guid.NewGuid());
        await Seed(nexus, eventId, first, second);
        await nexus.Create(
            CreateRanking(
                eventId,
                rankingId,
                "CEI 1*",
                new RankingEntry(first.Id, false),
                new RankingEntry(second.Id, false)
            )
        );
        var before = await ReadStored(MongoConstants.RANKINGS_COLLECTION, rankingId);

        first.Withdraw();
        await Send(HttpMethod.Patch, "api/participations", ParticipationModel.MapFrom(first));

        Assert.True((await nexus.ReadParticipation(eventId, first.Id)).IsEliminated()); // the change was written
        Assert.Equal(before, await ReadStored(MongoConstants.RANKINGS_COLLECTION, rankingId)); // and the Ranking was not
    }

    [Fact]
    public async Task A_custom_Ranking_can_still_be_created_edited_and_deleted()
    {
        using var nexus = new NexusApiDriver(_fixture.NexusBaseUrl);
        var eventId = Guid.NewGuid();
        var rankingId = Guid.NewGuid();
        var first = IntegrationPayloadFactory.ActiveParticipation(eventId, 1, Guid.NewGuid());
        var second = IntegrationPayloadFactory.ActiveParticipation(eventId, 2, Guid.NewGuid());
        await Seed(nexus, eventId, first, second);
        await nexus.Create(
            CreateRanking(
                eventId,
                rankingId,
                "Custom",
                new RankingEntry(first.Id, false),
                new RankingEntry(second.Id, false)
            )
        );

        await nexus.Update(CreateRanking(eventId, rankingId, "Custom, edited", new RankingEntry(first.Id, true)));

        var edited = await ReadStored(MongoConstants.RANKINGS_COLLECTION, rankingId);
        Assert.Equal("Custom, edited", edited["Name"].AsString);
        var entry = Assert.Single(edited["Entries"].AsBsonArray);
        AssertStandardUuid(entry["ParticipationId"], first.Id);
        Assert.True(entry["IsNotRanked"].AsBoolean);

        await Send(HttpMethod.Delete, $"api/rankings/{rankingId}");

        Assert.Empty(await nexus.ReadRankings(eventId));
    }

    static async Task Seed(NexusApiDriver nexus, Guid eventId, params Participation[] participations)
    {
        await nexus.Create(IntegrationPayloadFactory.EventInformation(eventId));
        foreach (var participation in participations)
        {
            await nexus.Create(participation);
        }
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

    static void AssertStandardUuid(BsonValue value, Guid expected)
    {
        Assert.Equal(BsonType.Binary, value.BsonType);
        Assert.Equal(BsonBinarySubType.UuidStandard, value.AsBsonBinaryData.SubType);
        Assert.Equal(expected, value.AsBsonBinaryData.ToGuid(GuidRepresentation.Standard));
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

    async Task Send(HttpMethod method, string endpoint, object? payload = null)
    {
        using var client = new HttpClient { BaseAddress = _fixture.NexusBaseUrl };
        using var request = new HttpRequestMessage(method, endpoint);
        if (payload != null)
        {
            request.Content = new StringContent(payload.ToJson(), Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"{method} {endpoint} answered {(int)response.StatusCode}: {content}"
        );
    }

    async Task<IReadOnlyList<HandoutModel>> ReadHandoutsFrom(string endpoint)
    {
        using var client = new HttpClient { BaseAddress = _fixture.NexusBaseUrl };
        var content = await client.GetStringAsync(endpoint);
        var result = content.FromJson<Not.Structures.Result<IEnumerable<HandoutModel>>>();
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors));
        return result.Data!.ToArray();
    }
}
