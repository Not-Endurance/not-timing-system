using Not.Domain.Exceptions;
using NTS.Contracts.Core;
using NTS.Contracts.Socket;
using NTS.Domain.Access;
using NTS.Domain.Core.Aggregates;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// The Core views take their Event from the route and read it through the viewed-event provider (#630, ADR-0007): the Ui's
/// own provider over the Api and its hub, all real. A Historic Event is read by its id with no connection and never
/// disturbs the Live Event a viewer follows; a Live Event is the one the viewer is connected to.
/// </summary>
public sealed class ViewedEventTests : IClassFixture<NtsIntegrationFixture>
{
    static readonly TimeSpan PATIENCE = TimeSpan.FromSeconds(20);

    readonly NtsIntegrationFixture _fixture;

    public ViewedEventTests(NtsIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task A_Historic_Event_is_read_by_its_id_beside_the_Live_Event_the_viewer_follows_and_leaves_it_alone()
    {
        var tenant = await TenancySeed.TenantAsync(_fixture.MongoConnectionString);
        var now = DateTimeOffset.UtcNow;
        var liveId = await EventSeed.LiveAsync(_fixture.MongoConnectionString, tenant, null, now);
        await SeedParticipationsAsync(tenant, liveId, 11, 12, 13);
        var historicId = await EventSeed.HistoricAsync(_fixture.MongoConnectionString, tenant, null, now);
        var historic = await SeedParticipationsAsync(tenant, historicId, 1, 2);
        await EventSeed.RankingAsync(
            _fixture.MongoConnectionString,
            tenant,
            IntegrationPayloadFactory.Ranking(historicId, historic, Guid.NewGuid(), "Historic Ranking")
        );
        await EventSeed.OfficialAsync(
            _fixture.MongoConnectionString,
            tenant,
            IntegrationPayloadFactory.Official(historicId, null, Guid.NewGuid())
        );
        await using var viewer = new ViewerDriver(_fixture.ApiBaseUrl, _fixture.FunctionsBaseUrl, null, "viewer-views");
        await viewer.Start();
        await viewer.Connect(IntegrationPayloadFactory.EventInformation(liveId));
        await viewer.WaitForParticipation(13, _ => true, PATIENCE);
        var store = viewer.GetRequiredService<IParticipationStore>();
        var held = store.Participations;

        using var view = await viewer.GetRequiredService<IViewedEventProvider>().Open(historicId);

        Assert.NotNull(view);
        Assert.Equal(EventStage.Historic, view.Stage);
        Assert.Equal(historicId, view.Event.Id);
        Assert.Equal([1, 2], view.Participations.Select(x => x.Combination.Number).Order());
        Assert.Equal(["Historic Ranking"], view.Rankings.Select(x => x.Name));
        Assert.Single(view.Officials);
        Assert.Equal(
            historic.Select(x => x.Id),
            view.CreateDocument(view.Rankings[0]).Entries.Select(x => x.ParticipationId)
        );
        Assert.False(view.CanWrite);
        Assert.Throws<DomainException>(view.EnsureCanWrite);
        var socket = viewer.GetRequiredService<INtsSocketService>();
        Assert.True(socket.IsConnected);
        Assert.Equal(liveId, socket.Event?.Id);
        Assert.Same(held, store.Participations);
        Assert.Equal([11, 12, 13], store.Participations.Select(x => x.Combination.Number).Order());
    }

    [Fact]
    public async Task A_Historic_Event_is_opened_without_a_connection_and_does_not_make_one()
    {
        var tenant = await TenancySeed.TenantAsync(_fixture.MongoConnectionString);
        var historicId = await EventSeed.HistoricAsync(
            _fixture.MongoConnectionString,
            tenant,
            null,
            DateTimeOffset.UtcNow
        );
        await SeedParticipationsAsync(tenant, historicId, 1, 2);
        await using var viewer = new ViewerDriver(
            _fixture.ApiBaseUrl,
            _fixture.FunctionsBaseUrl,
            null,
            "viewer-record"
        );
        await viewer.Start();

        using var view = await viewer.GetRequiredService<IViewedEventProvider>().Open(historicId);

        Assert.Equal([1, 2], view!.Participations.Select(x => x.Combination.Number).Order());
        Assert.False(viewer.GetRequiredService<INtsSocketService>().IsConnected);
    }

    [Fact]
    public async Task A_Live_Event_is_the_one_the_viewer_follows_once_it_is_opened_and_cannot_be_written_to_by_an_anonymous_viewer()
    {
        var tenant = await TenancySeed.TenantAsync(_fixture.MongoConnectionString);
        var liveId = await EventSeed.LiveAsync(_fixture.MongoConnectionString, tenant, null, DateTimeOffset.UtcNow);
        await SeedParticipationsAsync(tenant, liveId, 21, 22);
        await using var viewer = new ViewerDriver(_fixture.ApiBaseUrl, _fixture.FunctionsBaseUrl, null, "viewer-live");
        await viewer.Start();

        using var view = await viewer.GetRequiredService<IViewedEventProvider>().Open(liveId);

        Assert.NotNull(view);
        Assert.Equal(EventStage.Live, view.Stage);
        Assert.Equal(liveId, viewer.GetRequiredService<INtsSocketService>().Event?.Id);
        Assert.Equal([21, 22], view.Participations.Select(x => x.Combination.Number).Order());
        Assert.False(view.CanWrite);
        Assert.Throws<DomainException>(view.EnsureCanWrite);
    }

    [Fact]
    public async Task An_Event_the_Api_does_not_have_is_not_opened()
    {
        await using var viewer = new ViewerDriver(_fixture.ApiBaseUrl, _fixture.FunctionsBaseUrl, null, "viewer-none");
        await viewer.Start();

        Assert.Null(await viewer.GetRequiredService<IViewedEventProvider>().Open(Guid.NewGuid()));
    }

    async Task<Participation[]> SeedParticipationsAsync(string tenant, Guid eventId, params int[] numbers)
    {
        var participations = numbers
            .Select(number => IntegrationPayloadFactory.ActiveParticipation(eventId, number, Guid.NewGuid()))
            .ToArray();
        foreach (var participation in participations)
        {
            await EventSeed.ParticipationAsync(_fixture.MongoConnectionString, tenant, participation);
        }

        return participations;
    }
}
