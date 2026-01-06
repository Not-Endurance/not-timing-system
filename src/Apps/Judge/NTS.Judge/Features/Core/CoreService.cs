using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Notify;
using Not.Safe;
using Not.Storage.JsonFile.Stores;
using NTS.Domain.Core.Aggregates;
using NTS.Judge.Blazor.Shared.Components.SidePanels;
using NTS.Judge.Features.Core.Reset;
using NTS.Judge.Features.Core.Start;
using NTS.Judge.Features.Warp;
using NTS.Judge.HTTP;
using NTS.Storage.Core;

namespace NTS.Judge.Features.Core;

public class CoreService : ObservableBehind, ICoreService
{
    readonly IEnumerable<ICoreState> _coreStates;
    readonly EventRpcContext _eventsRpcContext;
    readonly IStore<CoreState> _coreStore;
    readonly ICoreStarter _coreStarter;
    readonly IRepository<Ranking> _rankings;
    readonly IRepository<EnduranceEvent> _events;
    readonly IRepository<Participation> _participations;
    readonly IRepository<Official> _officials;
    readonly IArchiveRepository _archive;

    public CoreService(
        IEnumerable<ICoreState> coreStates,
        EventRpcContext eventsRpcContext,
        IStore<CoreState> coreStore,
        ICoreStarter coreStarter,
        IRepository<Ranking> rankings,
        IRepository<EnduranceEvent> events,
        IRepository<Participation> participations,
        IRepository<Official> officials,
        IArchiveRepository archive
    )
    {
        _coreStates = coreStates;
        _eventsRpcContext = eventsRpcContext;
        _coreStore = coreStore;
        _coreStarter = coreStarter;
        _rankings = rankings;
        _events = events;
        _participations = participations;
        _officials = officials;
        _archive = archive;
    }

    public bool IsStarted { get; private set; }

    protected override async Task<bool> PerformInitialization(params IEnumerable<object> arguments)
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
        await _eventsRpcContext.ResetEvent();
    }

    public async Task HardReset()
    {
        await _coreStore.Delete();
        await SoftReset();
        IsStarted = false;
        foreach (var state in _coreStates)
        {
            state.Reset();
        }
    }

    public async Task LoadArchive(int archiveId)
    {
        var entry = await _archive.GetEntry(archiveId);
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
        EmitChange();
    }
}
