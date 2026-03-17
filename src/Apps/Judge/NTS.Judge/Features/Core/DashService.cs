using Not.Application.CRUD.Ports;
using Not.Exceptions;
using Not.Injection;
using Not.Notify;
using Not.Structures;
using NTS.Application.Core;
using NTS.Application.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Setup.Services.StartValidation;
using NTS.Judge.Features.Core.State;

namespace NTS.Judge.Features.Core;

public class DashService : IDashService, ISingleton
{
    readonly INtsSocketService _socketService;
    readonly IEnumerable<ICoreDependentObservables> _coreDependentObservables;
    readonly ICoreState _coreState;
    readonly IStartBusiness _startDashboardBusiness;
    readonly IRepository<Ranking> _rankings;
    readonly IRepository<EnduranceEvent> _enduranceEvents;
    readonly IRepository<Participation> _participations;
    readonly IRepository<Official> _officials;
    readonly IRepository<ArchiveEntry> _archive;
    readonly INotifier _notifier;

    public DashService(
        INtsSocketService socketService,
        IEnumerable<ICoreDependentObservables> coreDependentObservables,
        ICoreState coreState,
        IStartBusiness startDashboardBusiness,
        IRepository<Ranking> rankings,
        IRepository<EnduranceEvent> events,
        IRepository<Participation> participations,
        IRepository<Official> officials,
        IRepository<ArchiveEntry> archive,
        INotifier notifier
    )
    {
        _socketService = socketService;
        _coreDependentObservables = coreDependentObservables;
        _coreState = coreState;
        _startDashboardBusiness = startDashboardBusiness;
        _rankings = rankings;
        _enduranceEvents = events;
        _participations = participations;
        _officials = officials;
        _archive = archive;
        _notifier = notifier;
    }

    public Task<Result<IReadOnlyList<StartValidationIssue>>> Validate(int upcomingEventId)
    {
        return _startDashboardBusiness.Validate(upcomingEventId);
    }

    public async Task Start(int upcomingEventId)
    {
        var validationResult = await _startDashboardBusiness.Validate(upcomingEventId);
        if (validationResult.Data?.Any() == true)
        {
            throw GuardHelper.Exception($"Cannot start with invalid state. Ensure to call {nameof(DashService)}.{nameof(Validate)}");
        }

        var endranceEvent = await _startDashboardBusiness.CreateEnduranceEvent(upcomingEventId);
        await _socketService.Connect(endranceEvent);
        await _startDashboardBusiness.StartEnduranceEvent(upcomingEventId);
    }

    public async Task Reset()
    {
        await _coreState.Reset();
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

        await _enduranceEvents.Create(entry.EnduranceEvent);
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

public interface IDashService
{
    Task<Result<IReadOnlyList<StartValidationIssue>>> Validate(int upcomingEventId);
    Task Start(int upcomingEventId);
    Task LoadArchive(int archiveId);
    Task Reset();
}
