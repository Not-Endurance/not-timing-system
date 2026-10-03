using NoTiming.Ui.Features.Core.Dashboard;
using NoTiming.Ui.Features.Core.Participations;
using NTS.Application.Arrivelists;
using NTS.Application.Presentlists;
using NTS.Application.Startlists;

namespace NTS.Tests.Unit.Application;

/// <summary>Every list and page is a view over the one store (#622, ADR-0006): the Event is read once, whoever asks.</summary>
public sealed class ParticipationViewsTests
{
    [Fact]
    public async Task Every_view_reads_through_the_store_so_the_Event_is_read_once_and_each_event_costs_one_read()
    {
        var repository = new ControlledParticipationRepository(
            ParticipationFixtures.Active(1),
            ParticipationFixtures.Arrived(2)
        );
        var store = new ParticipationStore(repository);
        var startlist = new StartlistService(store);
        var presentlist = new PresentlistService(store);
        var arrivelist = new ArrivelistService(store);
        var context = new ParticipationService(store);

        await Task.WhenAll(startlist.Load(), presentlist.Load(), arrivelist.Load(), context.Load());

        Assert.Equal(1, repository.ReadManyCalls); // one read of the Event for the four views
        Assert.Equal([1], arrivelist.Entries.Select(x => x.Number));
        Assert.Equal([2], presentlist.Entries.Select(x => x.Number));
        Assert.Equal([1, 2], context.Participations.Select(x => x.Combination.Number));

        var arrived = ParticipationFixtures.Arrived(1);
        repository.Store(arrived);
        await store.Handle(ParticipationFixtures.Changed(arrived), CancellationToken.None);

        Assert.Equal([TestId.Of(1)], repository.Reads); // one read by id for the four views
        Assert.Empty(arrivelist.Entries);
        Assert.Equal([1, 2], presentlist.Entries.Select(x => x.Number).Order());
    }
}
