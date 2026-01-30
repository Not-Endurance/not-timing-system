using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Notify;
using Not.Safe;
using NTS.Domain.Core.Aggregates;
using NTS.Judge.Features.Core.Reset;
using NTS.Judge.Features.Core.Start;
using NTS.Judge.Features.Warp;

namespace NTS.Judge.Features.Core;

public class CoreService : NStatefulService, ICoreService
{
    readonly IEnumerable<ICoreDependentObservables> _coreDependentObservables;
    readonly ICoreState _coreState;
    readonly JudgeSocketContext _eventsRpcContext;
    readonly ICoreStarter _coreStarter;
    readonly IRepository<Ranking> _rankings;
    readonly IRepository<EnduranceEvent> _events;
    readonly IRepository<Participation> _participations;
    readonly IRepository<Official> _officials;
    readonly IRepository<ArchiveEntry> _archive;

    public CoreService(
        IEnumerable<ICoreDependentObservables> coreDependentObservables,
        ICoreState coreState,
        JudgeSocketContext eventsRpcContext,
        ICoreStarter coreStarter,
        IRepository<Ranking> rankings,
        IRepository<EnduranceEvent> events,
        IRepository<Participation> participations,
        IRepository<Official> officials,
        IRepository<ArchiveEntry> archive
    )
    {
        _coreDependentObservables = coreDependentObservables;
        _coreState = coreState;
        _eventsRpcContext = eventsRpcContext;
        _coreStarter = coreStarter;
        _rankings = rankings;
        _events = events;
        _participations = participations;
        _officials = officials;
        _archive = archive;
    }

    public bool IsStarted { get; private set; }

    protected override async Task<bool> InitializeState()
    {
        var enduranceEvents = await _events.Read(0);
        IsStarted = enduranceEvents != null;
        return IsStarted;
    }

    public Task Start()
    {
        return SafeHelper.Run(SafeStart);
    }

    public async Task SoftReset()
    {
        await _eventsRpcContext.Disconnect();
    }

    public async Task HardReset()
    {
        await _coreState.Reset();
        await SoftReset();
        IsStarted = false;
        foreach (var observable in _coreDependentObservables)
        {
            observable.ResetState();
        }
        EmitChanged();
    }

    public async Task LoadArchive(int archiveId)
    {
        var entry = await _archive.Read(archiveId);
        if (entry == null)
        {
            NotifyHelper.Inform($"Archive with id '{archiveId}' does not exist");
            return;
        }
        await _events.Create(entry.EnduranceEvent);
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
        IsStarted = true;
    }

    async Task SafeStart()
    {
        // TODO: Ensure witness apps receive the participants list on Start (or before).
        // Currently you need to restart witness after start in order to fetch
        await _coreStarter.Start();
        IsStarted = true;
        EmitChanged();
    }
}
