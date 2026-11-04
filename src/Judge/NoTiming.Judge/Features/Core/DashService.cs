using NC.Exceptions;
using NC.Injection;
using NC.Structures;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Application.Core;
using NoTiming.Domain.Setup.Services.StartValidation;
using NoTiming.Judge.Features.Core.State;

namespace NoTiming.Judge.Features.Core;

public class DashService : IDashService, IScoped
{
    readonly INoTimingSocketService _socketService;
    readonly IActiveEventsContext _activeEventService;
    readonly IEnumerable<ICoreDependentObservables> _coreDependentObservables;
    readonly IEventInformationRepository _eventInformationRepository;
    readonly IConfigureEventService _configureEvents;

    public DashService(
        INoTimingSocketService socketService,
        IActiveEventsContext activeEventService,
        IEnumerable<ICoreDependentObservables> coreDependentObservables,
        IEventInformationRepository eventInformationRepository,
        IConfigureEventService configureEvents
    )
    {
        _socketService = socketService;
        _activeEventService = activeEventService;
        _coreDependentObservables = coreDependentObservables;
        _eventInformationRepository = eventInformationRepository;
        _configureEvents = configureEvents;
    }

    public Task<Result<IReadOnlyList<StartValidationIssue>>> Validate(int configureEventId)
    {
        return _configureEvents.Validate(configureEventId);
    }

    public async Task Start(int configureEventId)
    {
        var validationResult = await _configureEvents.Validate(configureEventId);
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

        var eventInformation = await _eventInformationRepository.Start(configureEventId);
        _activeEventService.Add(eventInformation);
        await _socketService.Connect(eventInformation);
    }

    public async Task Deactivate()
    {
        var eventId = _socketService.Event?.Id;
        await _eventInformationRepository.Deactivate();
        if (eventId != null)
        {
            _activeEventService.Remove(eventId.Value);
        }
        ResetCoreDependentObservables();
        if (_socketService.IsConnected)
        {
            await _socketService.Disconnect();
        }
    }

    public async Task Reset()
    {
        var eventId = _socketService.Event?.Id;
        await _eventInformationRepository.Reset();
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
