using NoTiming.Ui.Features.Core.Participations;
using NTS.Application.Arrivelists;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects.Payloads;

namespace NTS.Tests.Unit.Application;

/// <summary>The arrivelist is a view over the store (#622): what it shows follows what the store holds.</summary>
public sealed class ArrivelistServiceTests
{
    [Fact]
    public async Task A_started_Participation_is_on_the_arrivelist_until_it_arrives()
    {
        var repository = new ControlledParticipationRepository(ParticipationFixtures.Active(1));
        var store = new ParticipationStore(repository);
        var service = new ArrivelistService(store);
        await service.Load();
        Assert.Equal([1], service.Entries.Select(x => x.Number));
        var arrived = ParticipationFixtures.Arrived(1);
        repository.Store(arrived);

        await store.Handle(new ParticipationArrived(arrived), CancellationToken.None);

        Assert.Empty(service.Entries);
    }

    [Fact]
    public async Task An_eliminated_Participation_is_not_on_the_arrivelist_and_a_restored_one_is()
    {
        var repository = new ControlledParticipationRepository(ParticipationFixtures.Active(1));
        var store = new ParticipationStore(repository);
        var service = new ArrivelistService(store);
        await service.Load();
        var eliminated = ParticipationFixtures.Eliminated(1);
        repository.Store(eliminated);

        await store.Handle(new ParticipationEliminated(eliminated), CancellationToken.None);

        Assert.Empty(service.Entries);

        var restored = ParticipationFixtures.Active(1);
        repository.Store(restored);
        await store.Handle(new ParticipationRestored(restored), CancellationToken.None);

        Assert.Equal([1], service.Entries.Select(x => x.Number));
    }

    [Fact]
    public async Task What_the_store_held_before_the_view_was_made_is_on_the_arrivelist()
    {
        var store = new ParticipationStore(
            new ControlledParticipationRepository(
                ParticipationFixtures.Active(1),
                ParticipationFixtures.Completed(2),
                ParticipationFixtures.Eliminated(3)
            )
        );
        await store.Load();
        var service = new ArrivelistService(store);

        await service.Load();

        Assert.Equal([1], service.Entries.Select(x => x.Number)); // completed and eliminated ones are hidden by the view
    }
}
