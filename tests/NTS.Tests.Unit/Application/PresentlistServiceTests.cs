using NoTiming.Ui.Features.Core.Participations;
using NTS.Application.Presentlists;
using NTS.Domain.Core.Aggregates.Participations.Objects;

namespace NTS.Tests.Unit.Application;

/// <summary>The presentlist is a view over the store (#622): what it shows follows what the store holds.</summary>
public sealed class PresentlistServiceTests
{
    [Fact]
    public async Task A_Participation_that_arrived_is_on_the_presentlist_until_it_is_eliminated()
    {
        var repository = new ControlledParticipationRepository(ParticipationFixtures.Active(1));
        var store = new ParticipationStore(repository);
        var service = new PresentlistService(store);
        await service.Load();
        Assert.Empty(service.Entries);
        var arrived = ParticipationFixtures.Arrived(1);
        repository.Store(arrived);

        await store.Handle(ParticipationFixtures.Changed(arrived), CancellationToken.None);

        Assert.Equal([1], service.Entries.Select(x => x.Number));

        var eliminated = ParticipationFixtures.Arrived(1, new Withdrawn());
        repository.Store(eliminated);
        await store.Handle(ParticipationFixtures.Changed(eliminated), CancellationToken.None);

        Assert.Empty(service.Entries);
    }

    [Fact]
    public async Task What_the_store_held_before_the_view_was_made_is_on_the_presentlist()
    {
        var store = new ParticipationStore(
            new ControlledParticipationRepository(ParticipationFixtures.Arrived(1), ParticipationFixtures.Active(2))
        );
        await store.Load();
        var service = new PresentlistService(store);

        await service.Load();

        Assert.Equal([1], service.Entries.Select(x => x.Number));
    }

    [Fact]
    public async Task The_presentlist_tells_the_pages_when_the_store_changes()
    {
        var repository = new ControlledParticipationRepository(ParticipationFixtures.Active(1));
        var store = new ParticipationStore(repository);
        var service = new PresentlistService(store);
        await service.Load();
        var changes = 0;
        service.ObservableEvent.Subscribe(() => changes++);
        var arrived = ParticipationFixtures.Arrived(1);
        repository.Store(arrived);

        await store.Handle(ParticipationFixtures.Changed(arrived), CancellationToken.None);

        Assert.Equal(1, changes);
    }
}
