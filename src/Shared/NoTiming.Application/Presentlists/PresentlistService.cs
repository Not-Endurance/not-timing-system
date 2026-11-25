using MediatR;
using NC.Application.Behinds.Adapters;
using NC.Collections;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Presentlists;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Aggregates.Participations.Objects;
using NoTiming.Domain.Core.Events;
using NoTiming.Domain.Core.Objects.Presentlists;

namespace NoTiming.Application.Presentlists;

public class PresentlistService
    : NStatefulService,
        IPresentlistService,
        INotificationHandler<ParticipationChanged>,
        INotificationHandler<EventConnected>,
        INotificationHandler<EventDisconnected>
{
    readonly IEventScopedRepository<Participation> _participations;
    readonly INoTimingSocketContext? _socketContext;
    readonly IPresentlistActionPublisher? _actionPublisher;
    readonly IPresentlistAccess? _access;
    UniqueParticipations _state = new();

    public PresentlistService(IEventScopedRepository<Participation> participations)
        : this(participations, null, [], []) { }

    public PresentlistService(
        IEventScopedRepository<Participation> participations,
        INoTimingSocketContext? socketContext,
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
        _state = [.. participations];
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
                // Only this client's list changes. Other clients keep the entry until they reload.
                Presentlist = Presentlist.Without(new PresentlistEntryKey(entry.Number, entry.PhaseId, entry.Type));
                EmitChanged();
                break;
        }
    }

    public Task Handle(ParticipationChanged notification, CancellationToken cancellationToken)
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
