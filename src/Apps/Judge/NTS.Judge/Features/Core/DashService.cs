using Not.Application.CRUD.Ports;
using Not.Domain.Exceptions;
using Not.Exceptions;
using Not.Injection;
using Not.Notify;
using Not.Structures;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.Socket;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Setup.Services.StartValidation;
using NTS.Judge.Contracts.Features.Setup.UpcomingEvents;
using NTS.Judge.Features.Core.State;

namespace NTS.Judge.Features.Core;

public class DashService : IDashService, IScoped
{
    readonly INtsSocketService _socketService;
    readonly IActiveEventsContext _activeEventService;
    readonly IEnumerable<ICoreDependentObservables> _coreDependentObservables;
    readonly IEnduranceEventRepository _enduranceEvents;
    readonly IUpcomingEventService _upcomingEvents;
    readonly IRepository<Ranking> _rankings;
    readonly IRepository<EnduranceEvent> _events;
    readonly IRepository<Participation> _participations;
    readonly IRepository<Official> _officials;
    readonly IRepository<ArchiveEntry> _archive;
    readonly INotifier _notifier;

    public DashService(
        INtsSocketService socketService,
        IActiveEventsContext activeEventService,
        IEnumerable<ICoreDependentObservables> coreDependentObservables,
        IEnduranceEventRepository enduranceEvents,
        IUpcomingEventService upcomingEvents,
        IRepository<Ranking> rankings,
        IRepository<EnduranceEvent> events,
        IRepository<Participation> participations,
        IRepository<Official> officials,
        IRepository<ArchiveEntry> archive,
        INotifier notifier
    )
    {
        _socketService = socketService;
        _activeEventService = activeEventService;
        _coreDependentObservables = coreDependentObservables;
        _enduranceEvents = enduranceEvents;
        _upcomingEvents = upcomingEvents;
        _rankings = rankings;
        _events = events;
        _participations = participations;
        _officials = officials;
        _archive = archive;
        _notifier = notifier;
    }

    public Task<Result<IReadOnlyList<StartValidationIssue>>> Validate(int upcomingEventId)
    {
        return _upcomingEvents.Validate(upcomingEventId);
    }

    public async Task Start(int upcomingEventId)
    {
        var validationResult = await _upcomingEvents.Validate(upcomingEventId);
        if (validationResult.Data?.Any() == true)
        {
            throw GuardHelper.Exception(
                $"Cannot start with invalid state. Ensure to call {nameof(DashService)}.{nameof(Validate)}"
            );
        }
        if (_socketService.IsConnected)
        {
            await _socketService.Disconnect();
        }

        var enduranceEvent = await _enduranceEvents.Start(upcomingEventId);
        _activeEventService.Add(enduranceEvent);
        await _socketService.Connect(enduranceEvent);
    }

    public async Task Reset()
    {
        var eventId = _socketService.Event?.Id;
        await _enduranceEvents.Reset();
        if (eventId != null)
        {
            _activeEventService.Remove(eventId.Value);
        }
        ResetCoreDependentObservables();
    }

    public async Task LoadArchive(int archiveId)
    {
        var entry = await _archive.Read(archiveId);
        if (entry == null)
        {
            _notifier.Inform($"Archive with id '{archiveId}' does not exist");
            return;
        }

        await _events.Create(entry.EnduranceEvent);
        await _socketService.Connect(entry.EnduranceEvent);
        foreach (var official in entry.Officials)
        {
            await _officials.Create(official);
        }
        foreach (var ranklist in entry.Ranklists)
        {
            await _rankings.Create(ranklist.Ranking);
        }
        foreach (var participation in entry.Ranklists.SelectMany(x => x.Entries).Select(x => x.Participation))
        {
            await _participations.Create(participation);
        }
        ResetCoreDependentObservables();
    }

    void ResetCoreDependentObservables()
    {
        // TODO: replace with events when implement domain event dispatcher and handlers
        foreach (var observable in _coreDependentObservables)
        {
            observable.ResetHasLoaded();
        }
    }
}
