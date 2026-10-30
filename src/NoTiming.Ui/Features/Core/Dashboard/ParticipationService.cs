using MediatR;
using Not.Application.Behinds.Adapters;
using Not.Injection;
using NTS.Contracts.Core;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;

namespace NoTiming.Ui.Features.Core.Dashboard;

/// <summary>
/// What the dashboard pages share: the Participations that are still to be timed, as a view over the store (ADR-0006),
/// and the one the person selected, which is page state and stays here.
/// </summary>
public class ParticipationService
    : NStatefulService,
        IParticipationContext,
        INotificationHandler<EventDisconnected>,
        IScoped
{
    readonly IParticipationStore _store;
    Guid? _selected;

    public ParticipationService(IParticipationStore store)
    {
        _store = store;
        Observe(store, Rebuild);
    }

    public Participation? Selected
    {
        get => _selected is { } id ? _store.Find(id) : null;
        set
        {
            _selected = value?.Id;
            EmitChanged();
        }
    }

    public IReadOnlyList<Participation> Participations { get; private set; } = [];
    public IReadOnlyList<int> RecentlyTimed { get; } = [];

    protected override async Task<bool> InitializeState()
    {
        await _store.Load();
        Participations = Active();
        return Participations.Any();
    }

    public Task Handle(EventDisconnected notification, CancellationToken cancellationToken)
    {
        _selected = null;
        Participations = [];
        ClearState();
        return Task.CompletedTask;
    }

    void Rebuild()
    {
        Participations = Active();
        if (_selected is { } id && IsGone(_store.Find(id)))
        {
            _selected = null; // an eliminated Participation is not selected any more, nor again when it is restored
        }

        EmitChanged();
    }

    IReadOnlyList<Participation> Active()
    {
        return [.. _store.Participations.Where(x => !x.IsComplete() && !x.IsEliminated())];
    }

    static bool IsGone(Participation? participation)
    {
        return participation is null || participation.IsEliminated();
    }
}
