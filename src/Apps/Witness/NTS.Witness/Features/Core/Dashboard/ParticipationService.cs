using MediatR;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Collections;
using Not.Injection;
using Not.Observables.Structures;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects.Payloads;

namespace NTS.Witness.Features.Core.Dashboard;

public class ParticipationService
    : NStatefulService<ObservableList<Participation>>,
        IParticipationContext,
        INotificationHandler<PhaseCompleted>,
        INotificationHandler<ParticipationEliminated>,
        INotificationHandler<ParticipationRestored>,
        INotificationHandler<EventConnected>,
        INotificationHandler<EventDisconnected>,
        IScoped
{
    readonly IReadMany<Participation> _participationReader;
    readonly INtsSocketContext? _socketContext;
    Participation? _selected;

    public ParticipationService(IReadMany<Participation> participationReader)
        : this(participationReader, null) { }

    public ParticipationService(IReadMany<Participation> participationReader, INtsSocketContext? socketContext)
    {
        _participationReader = participationReader;
        _socketContext = socketContext;
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
        if (_socketContext?.Event == null && _socketContext != null)
        {
            State.Clear();
            return false;
        }

        var participations = await _participationReader.ReadMany(x => !x.IsComplete() && !x.IsEliminated());
        State.ClearAndAddRange(participations);
        return State.Any();
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

    void Update(Participation participation, NCollectionAction action)
    {
        State.Update(participation, action);
        if (_selected == null)
        {
            return;
        }

        _selected = State.FirstOrDefault(x => x.Id == _selected.Id);
    }
}
