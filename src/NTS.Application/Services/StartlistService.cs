using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Startup;
using NTS.Blazor.Components.Startlist.History;
using NTS.Blazor.Components.Startlist.Upcoming;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Application.Services;

public class StartlistService : ObservableBehind, IStartUpcoming, IStartHistory, IStartupInitializer
{
    readonly IRepository<Participation> _participationRepository;
    Startlist? _startlist;

    public StartlistService(IRepository<Participation> participationRepository)
    {
        _participationRepository = participationRepository;
    }

    public IReadOnlyList<StartlistEntry> Upcoming => _startlist?.Upcoming ?? [];
    public IReadOnlyList<StartlistEntry> History => _startlist?.History ?? [];

    protected override async Task<bool> PerformInitialization(params IEnumerable<object> arguments)
    {
        var participations = await _participationRepository.ReadAll();
        _startlist = new Startlist(participations);

        return _startlist.History.Any() || _startlist.Upcoming.Any();
    }

    public void RunAtStartup()
    {
        Participation.PHASE_COMPLETED_EVENT.Subscribe(x => AddEntry(x.Participation));
        Participation.RESTORED_EVENT.Subscribe(x => AddEntry(x.Participation));
        Participation.ELIMINATED_EVENT.Subscribe(x => RemoveEntry(x.Participation));
    }

    public void Refresh()
    {
        _startlist?.UpdateState();
        EmitChange();
    }

    void RemoveEntry(Participation participation)
    {
        _startlist?.Remove(participation.Combination.Number);
        EmitChange();
    }

    void AddEntry(Participation participation)
    {
        _startlist?.Add(participation);
        EmitChange();
    }
}
