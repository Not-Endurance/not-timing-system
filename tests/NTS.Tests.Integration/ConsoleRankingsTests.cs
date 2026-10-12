using MongoDB.Bson;
using MongoDB.Driver;
using Not.Domain.Exceptions;
using Not.Events;
using Not.Krud.Abstractions;
using NoTiming.Ui.Features.Console;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Console.Rankings;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The Rankings of a Live Event as the Main Operator changes them (#646, ADR-0006): made of the Participations it chooses,
/// stored by the Api for the Event of the view, and removed. Over the real Api in this process.
/// </summary>
public sealed class ConsoleRankingsTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public ConsoleRankingsTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_Ranking_of_chosen_Participations_is_stored_for_the_Event_of_the_view_which_reads_its_Rankings_again()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api);
        var rankings = scene.Open();

        await ((IKrudFormService<CustomRankingModel>)rankings).Create(scene.Model("Open class", 2));

        var kept = Assert.Single(await StoredAsync(scene.EventId));
        Assert.Equal("Open class", kept["Name"].AsString);
        Assert.Equal(2, kept["Entries"].AsBsonArray.Count);
        Assert.Equal(1, scene.View.Refreshes);
    }

    [Fact]
    public async Task A_Ranking_is_removed_by_the_Main_Operator_and_the_view_reads_its_Rankings_again()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api);
        var rankings = scene.Open();
        var model = scene.Model("Open class", 1);
        await ((IKrudFormService<CustomRankingModel>)rankings).Create(model);

        await rankings.Delete(model.MapToEntity()); // the model keeps the id the Ranking was made with

        Assert.Empty(await StoredAsync(scene.EventId));
        Assert.Equal(2, scene.View.Refreshes);
    }

    [Fact]
    public async Task A_Ranking_that_the_Api_does_not_take_is_said_in_its_words_and_nothing_is_stored()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api, mainOperator: false);
        var rankings = scene.Open();

        var refused = await Assert.ThrowsAsync<DomainException>(
            () => ((IKrudFormService<CustomRankingModel>)rankings).Create(scene.Model("Open class", 1))
        );

        Assert.NotEmpty(refused.Message);
        Assert.Contains(' ', refused.Message); // the words of the Api, and not the code it answered with
        Assert.Empty(await StoredAsync(scene.EventId));
        Assert.Equal(1, scene.View.Refreshes); // what is shown is read again from what is stored
    }

    [Fact]
    public async Task A_view_that_cannot_write_refuses_before_the_Api_is_asked()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api, canWrite: false);
        var rankings = scene.Open();
        var model = scene.Model("Open class", 1);

        await Assert.ThrowsAsync<DomainException>(() => ((IKrudFormService<CustomRankingModel>)rankings).Create(model));
        await Assert.ThrowsAsync<DomainException>(() => rankings.Delete(model.MapToEntity()));

        Assert.Empty(
            scene.Requests.Asked.Where(x =>
                x.StartsWith("POST", StringComparison.Ordinal) || x.StartsWith("DELETE", StringComparison.Ordinal)
            )
        );
        Assert.Equal(0, scene.View.Refreshes);
    }

    async Task<Scene> SceneAsync(ApiFactory api, bool mainOperator = true, bool canWrite = true)
    {
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var boss = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var person = mainOperator ? boss : await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, boss.Id, DateTimeOffset.UtcNow);
        var participations = new List<Participation>();
        foreach (var number in new[] { 1, 2 })
        {
            var participation = IntegrationPayloadFactory.ActiveParticipation(eventId, number, Guid.NewGuid());
            await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation);
            participations.Add(participation);
        }

        return new Scene(api, person, eventId, participations, canWrite);
    }

    async Task<List<BsonDocument>> StoredAsync(Guid eventId)
    {
        var filter = new BsonDocument("EventId", EventSeed.Binary(eventId));
        return await RegistrySeed.Collection(_mongo.ConnectionString, "event_rankings").Find(filter).ToListAsync();
    }

    sealed class Scene
    {
        readonly ApiFactory _api;
        readonly Person _person;

        public Scene(ApiFactory api, Person person, Guid eventId, List<Participation> participations, bool canWrite)
        {
            _api = api;
            _person = person;
            EventId = eventId;
            Participations = participations;
            View = new StubViewedEvent(
                new FixedStore(participations),
                IntegrationPayloadFactory.EventInformation(eventId),
                canWrite
            );
        }

        public Guid EventId { get; }
        public List<Participation> Participations { get; }
        public StubViewedEvent View { get; }
        public JsonApiClients.Requests Requests { get; private set; } = new();

        public ConsoleRankings Open()
        {
            var json = JsonApiClients.Of(_api, _person, out var requests);
            Requests = requests;
            var rankings = new ConsoleRankings(new ConsoleRankingWrites(json));
            rankings.Show(View);
            return rankings;
        }

        public CustomRankingModel Model(string name, int participations)
        {
            return new CustomRankingModel
            {
                Name = name,
                Ruleset = CompetitionRuleset.FEI,
                Category = ParticipationCategory.Senior,
                Entries =
                [
                    .. Participations
                        .Take(participations)
                        .Select(x => new CustomRankingEntryModel { Participation = x }),
                ],
            };
        }
    }

    sealed class FixedStore : IParticipationStore
    {
        readonly IReadOnlyList<Participation> _participations;

        public FixedStore(IReadOnlyList<Participation> participations)
        {
            _participations = participations;
        }

        public IEventSubscriber ObservableEvent { get; } = new Event();
        public IReadOnlyList<Participation> Participations => _participations;

        public Task Load()
        {
            return Task.CompletedTask;
        }

        public Participation? Find(Guid id)
        {
            return _participations.FirstOrDefault(x => x.Id == id);
        }

        public Task Refresh(Guid id)
        {
            return Task.CompletedTask;
        }
    }
}
