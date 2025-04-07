using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Notify;
using Not.Safe;
using NTS.Domain.Core.Aggregates;
using NTS.Judge.Blazor.Shared.Components.SidePanels;
using NTS.Judge.Core.Start;
using NTS.Judge.HTTP;

namespace NTS.Judge.Core.Behinds.Adapters;

public class CoreBehind : ObservableBehind, ICoreBehind
{
    readonly ICoreStarter _coreStarter;
    readonly IRepository<Ranking> _rankings;
    readonly IRepository<EnduranceEvent> _events;
    readonly IRepository<Participation> _participations;
    readonly IRepository<Official> _officials;
    readonly IArchiveRepository _archive;

    public CoreBehind(
        ICoreStarter coreStarter,
        IRepository<Ranking> rankings,
        IRepository<EnduranceEvent> events,
        IRepository<Participation> participations,
        IRepository<Official> officials,
        IArchiveRepository archive
    )
    {
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
