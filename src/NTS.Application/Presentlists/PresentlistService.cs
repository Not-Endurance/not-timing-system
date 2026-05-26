using MediatR;
using Not.Application.Behinds.Adapters;
using Not.Collections;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Presentlists;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Core.Objects.Presentlists;

namespace NTS.Application.Presentlists;

public class PresentlistService
    : NStatefulService,
        IPresentlistService,
        INotificationHandler<ParticipationArrived>,
        INotificationHandler<PhaseCompleted>,
        INotificationHandler<InspectionRequired>,
        INotificationHandler<RepresentationRequired>,
        INotificationHandler<ParticipationRestored>,
        INotificationHandler<ParticipationEliminated>,
        INotificationHandler<VetInAcknoledged>,
        INotificationHandler<EventConnected>,
        INotificationHandler<EventDisconnected>
{
    readonly IEventScopedRepository<Participation> _participations;
    readonly INtsSocketContext? _socketContext;
    readonly IPresentlistActionPublisher? _actionPublisher;
    readonly IPresentlistAccess? _access;
    UniqueParticipations _state = new();

    public PresentlistService(IEventScopedRepository<Participation> participations)
        : this(participations, null, [], []) { }

    public PresentlistService(
        IEventScopedRepository<Participation> participations,
        INtsSocketContext? socketContext,
        IEnumerable<IPresentlistActionPublisher> actionPublishers,
        IEnumerable<IPresentlistAccess> accessPolicies
    )
    {
        _participations = participations;
        _socketContext = socketContext;
        _actionPublisher = actionPublishers.FirstOrDefault();
        _access = accessPolicies.FirstOrDefault();
    }

    public Presentlist Presentlist { get; private set; } = new([]);

    public IReadOnlyList<PresentlistEntry> Entries => Presentlist.Entries;

    public bool CanAcknowledge => _actionPublisher != null && _access?.CanAcknowledgePresentations == true;

    protected override async Task<bool> InitializeState()
    {
        if (_socketContext?.Event == null && _socketContext != null)
        {
            _state.Clear();
            Presentlist = new Presentlist([]);
            return false;
        }

        var participations = await _participations.ReadMany(x => !x.IsEliminated());
        _state = [..participations];
        Presentlist = new Presentlist(_state);
        return Entries.Any();
    }

    public async Task Acknowledge(PresentlistEntry entry)
    {
        if (!CanAcknowledge || _actionPublisher == null)
        {
            return;
        }

        switch (entry.Type)
        {
            case PresentlistEntryType.Present:
            case PresentlistEntryType.Represent:
                await _actionPublisher.PublishPresentation(entry);
                break;
            case PresentlistEntryType.RI:
            case PresentlistEntryType.CRI:
                var acknoledgement = new VetInAcknoledged(
                    entry.Number,
                    entry.PhaseId,
                    entry.Type,
                    entry.Time
                );
                await _actionPublisher.PublishPresentationAcknoledged(acknoledgement);
                break;
        }
    }

    public Task Handle(ParticipationArrived notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation, NCollectionAction.AddOrUpdate);
        return Task.CompletedTask;
    }

    public Task Handle(PhaseCompleted notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation, NCollectionAction.AddOrUpdate);
        return Task.CompletedTask;
    }

    public Task Handle(InspectionRequired notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation, NCollectionAction.AddOrUpdate);
        return Task.CompletedTask;
    }

    public Task Handle(RepresentationRequired notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation, NCollectionAction.AddOrUpdate);
        return Task.CompletedTask;
    }

    public Task Handle(ParticipationRestored notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation, NCollectionAction.AddOrUpdate);
        return Task.CompletedTask;
    }

    public Task Handle(ParticipationEliminated notification, CancellationToken cancellationToken)
    {
        Update(notification.Participation, NCollectionAction.Remove);
        return Task.CompletedTask;
    }

    public Task Handle(VetInAcknoledged notification, CancellationToken cancellationToken)
    {
        Presentlist = Presentlist.Without(notification.Key);
        EmitChanged();
        return Task.CompletedTask;
    }

    public async Task Handle(EventConnected notification, CancellationToken cancellationToken)
    {
        await ReloadState();
    }

    public Task Handle(EventDisconnected notification, CancellationToken cancellationToken)
    {
        _state.Clear();
        Presentlist = new Presentlist([]);
        ClearState();
        return Task.CompletedTask;
    }

    public void Tick()
    {
        EmitChanged();
    }

    void Update(Participation participation, NCollectionAction action)
    {
        switch (action)
        {
            case NCollectionAction.AddOrUpdate:
                if (!participation.IsEliminated())
                {
                    _state.Upsert(participation);
                    Presentlist = Presentlist.With(participation);
                }
                else
                {
                    _state.Remove(participation);
                    Presentlist = Presentlist.WithoutParticipation(participation.Combination.Number);
                }
                break;
            case NCollectionAction.Remove:
                _state.Remove(participation);
                Presentlist = Presentlist.WithoutParticipation(participation.Combination.Number);
                break;
        }
        EmitChanged();
    }
}
