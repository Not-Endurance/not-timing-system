using Not.Exceptions;
using Not.Injection;
using Not.Structures;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Socket;
using NTS.Application.Core;
using NTS.Domain.Setup.Services.StartValidation;
using NTS.Judge.Features.Core.State;

namespace NTS.Judge.Features.Core;

public class DashService : IDashService, IScoped
{
    readonly INtsSocketService _socketService;
    readonly IActiveEventsContext _activeEventService;
    readonly IEnumerable<ICoreDependentObservables> _coreDependentObservables;
    readonly IEnduranceEventRepository _enduranceEvents;
    readonly IUpcomingEventService _upcomingEvents;

    public DashService(
        INtsSocketService socketService,
        IActiveEventsContext activeEventService,
        IEnumerable<ICoreDependentObservables> coreDependentObservables,
        IEnduranceEventRepository enduranceEvents,
        IUpcomingEventService upcomingEvents
    )
    {
        _socketService = socketService;
        _activeEventService = activeEventService;
        _coreDependentObservables = coreDependentObservables;
        _enduranceEvents = enduranceEvents;
        _upcomingEvents = upcomingEvents;
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

    void ResetCoreDependentObservables()
    {
        // TODO: replace with events when implement domain event dispatcher and handlers
        foreach (var observable in _coreDependentObservables)
        {
            observable.ResetHasLoaded();
        }
    }
}
