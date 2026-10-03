using NoTiming.Ui.Features.Core.Dashboard;
using NoTiming.Ui.Features.Core.Participations;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects.Payloads;

namespace NTS.Tests.Unit.Application;

/// <summary>The participation context of the dashboard is a view over the store (#622); the selection is its own.</summary>
public sealed class ParticipationContextTests
{
    [Fact]
    public async Task The_context_lists_the_Participations_that_are_neither_completed_nor_eliminated()
    {
        var store = new ParticipationStore(
            new ControlledParticipationRepository(
                ParticipationFixtures.Active(1),
                ParticipationFixtures.Completed(2),
                ParticipationFixtures.Eliminated(3),
                ParticipationFixtures.Arrived(4)
            )
        );
        var context = new ParticipationService(store);

        await context.Load();

        Assert.Equal([1, 4], context.Participations.Select(x => x.Combination.Number));
    }

    [Fact]
    public async Task The_selected_Participation_is_the_one_the_store_holds_now_and_is_dropped_when_it_is_eliminated()
    {
        var repository = new ControlledParticipationRepository(
            ParticipationFixtures.Active(1),
            ParticipationFixtures.Active(2)
        );
        var store = new ParticipationStore(repository);
        var context = new ParticipationService(store);
        await context.Load();
        context.Selected = store.Find(TestId.Of(1));
        var arrived = ParticipationFixtures.Arrived(1);
        repository.Store(arrived);

        await store.Handle(new ParticipationArrived(arrived), CancellationToken.None);

        Assert.Same(arrived, context.Selected); // not the instance that was selected: the one the store holds now

        var eliminated = ParticipationFixtures.Eliminated(1);
        repository.Store(eliminated);
        await store.Handle(new ParticipationEliminated(eliminated), CancellationToken.None);

        Assert.Null(context.Selected);

        var restored = ParticipationFixtures.Active(1);
        repository.Store(restored);
        await store.Handle(new ParticipationRestored(restored), CancellationToken.None);

        Assert.Null(context.Selected); // a restored Participation is not selected again by itself
    }

    [Fact]
    public async Task Selecting_and_leaving_the_Event_tell_the_pages_and_clear_the_selection()
    {
        var store = new ParticipationStore(new ControlledParticipationRepository(ParticipationFixtures.Active(1)));
        var context = new ParticipationService(store);
        await context.Load();
        var changes = 0;
        context.ObservableEvent.Subscribe(() => changes++);

        context.Selected = store.Find(TestId.Of(1));

        Assert.Equal(1, changes);

        await store.Handle(new EventDisconnected(TestId.Of(100)), CancellationToken.None);
        await context.Handle(new EventDisconnected(TestId.Of(100)), CancellationToken.None);

        Assert.Null(context.Selected);
        Assert.Empty(context.Participations);
    }
}
