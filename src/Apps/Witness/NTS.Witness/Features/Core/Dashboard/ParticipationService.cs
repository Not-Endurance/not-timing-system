using MediatR;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Collections;
using Not.Injection;
using Not.Observables.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Objects;

namespace NTS.Witness.Features.Core.Dashboard;

public class ParticipationService
    : NStatefulService<ObservableList<Participation>>,
        IParticipationService,
        IPerformanceService,
        INotificationHandler<PhaseCompleted>,
        INotificationHandler<ParticipationEliminated>,
        INotificationHandler<ParticipationRestored>,
        INotificationHandler<EventConnected>,
        INotificationHandler<EventDisconnected>,
        ISingleton
{
    readonly IReadMany<Participation> _participationReader;
    Participation? _selected;

    public ParticipationService(IReadMany<Participation> participationReader)
    {
        _participationReader = participationReader;
    }

    public Participation? Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            EmitChanged();
        }
    }

    public IReadOnlyList<Participation> Participations => State;
    public IReadOnlyList<int> RecentlyTimed { get; } = [];

    protected override async Task<bool> InitializeState()
    {
        var participations = await _participationReader.ReadMany(x => !x.IsComplete() && !x.IsEliminated());
        State.ClearAndAddRange(participations);
        return State.Any();
    }

    public void Update(Participation participation, NCollectionAction action)
    {
        State.Update(participation, action);
    }

    public IEnumerable<Person> GetPeople()
    {
        return Participations.Select(p => p.Combination.Athlete.Names).Distinct();
    }

    public Participation GetParticipation(Person person)
    {
        return Participations.First(p => p.Combination.Athlete.Names.Equals(person));
    }

    public Task Handle(PhaseCompleted notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation, NCollectionAction.AddOrUpdate);
        return Task.CompletedTask;
    }

    public Task Handle(ParticipationEliminated notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation, NCollectionAction.Remove);
        return Task.CompletedTask;
    }

    public Task Handle(ParticipationRestored notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation, NCollectionAction.AddOrUpdate);
        return Task.CompletedTask;
    }

    public async Task Handle(EventConnected notification, CancellationToken cancellationToken)
    {
        await ReloadState();
    }

    public Task Handle(EventDisconnected notification, CancellationToken cancellationToken)
    {
        _selected = null;
        State.Clear();
        ClearState();
        return Task.CompletedTask;
    }
}
