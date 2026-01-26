using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Startup;
using NTS.Blazor.Components.Startlist.History;
using NTS.Blazor.Components.Startlist.Upcoming;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Application.Startlists;

public class StartlistService : NStatefulService, IStartUpcoming, IStartHistory, IStartupInitializer
{
    readonly IReadMany<Participation> _participations;
    readonly StartlistContext _context;

    public StartlistService(IReadMany<Participation> participations, StartlistContext context)
    {
        _participations = participations;
        _context = context;
    }

    public IReadOnlyList<StartlistEntry> Upcoming => _context.Startlist?.Upcoming ?? [];
    public IReadOnlyList<StartlistEntry> History => _context.Startlist?.History ?? [];

    protected override async Task<bool> InitializeState()
    {
        var participations = await _participations.ReadMany();
        _context.Startlist = new Startlist(participations);
        return _context.Startlist.History.Any() || _context.Startlist.Upcoming.Any();
    }

    public void RunAtStartup()
    {
        Participation.PHASE_COMPLETED_EVENT.Subscribe(x => AddEntry(x.Participation));
        Participation.RESTORED_EVENT.Subscribe(x => AddEntry(x.Participation));
        Participation.ELIMINATED_EVENT.Subscribe(x => RemoveEntry(x.Participation)); 
    }

    public void Refresh()
    {
        _context.Startlist?.UpdateState();
        EmitChanged();
    }

    void RemoveEntry(Participation participation)
    {
        _context.Startlist?.Remove(participation.Combination.Number);
        EmitChanged();
    }

    void AddEntry(Participation participation)
    {
        _context.Startlist?.Add(participation);
        EmitChanged();
    }
}
