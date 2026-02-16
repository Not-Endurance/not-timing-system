using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Collections;
using Not.Startup;
using NTS.Blazor.Components.Startlist.History;
using NTS.Blazor.Components.Startlist.Upcoming;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Application.Startlists;

public class StartlistService : NStatefulService, IStartUpcoming, IStartHistory, IStartupInitializer, IStartlistContext
{
    readonly IReadMany<Participation> _participations;

    public StartlistService(IReadMany<Participation> participations)
    {
        _participations = participations;
    }
    public Startlist? Startlist { get; set; }

    public IReadOnlyList<StartlistEntry> Upcoming => Startlist?.Upcoming ?? [];
    public IReadOnlyList<StartlistEntry> History => Startlist?.History ?? [];

    protected override async Task<bool> InitializeState()
    {
        var participations = await _participations.ReadMany();
        Startlist = new Startlist(participations);
        return Startlist.History.Any() || Startlist.Upcoming.Any();
    }

    public void Update(Participation participation, NCollectionAction action)
    {
        switch (action)
        {
            case NCollectionAction.Remove:
                RemoveEntry(participation);
                break;
            case NCollectionAction.AddOrUpdate:
                AddEntry(participation);
                break;
            default:
                break;
        }
    }

    public void RunAtStartup()
    {
        Participation.PHASE_COMPLETED_EVENT.Subscribe(x => AddEntry(x.Participation));
        Participation.RESTORED_EVENT.Subscribe(x => AddEntry(x.Participation));
        Participation.ELIMINATED_EVENT.Subscribe(x => RemoveEntry(x.Participation));
    }

    public void Refresh()
    {
        Startlist?.UpdateState();
        EmitChanged();
    }

    void RemoveEntry(Participation participation)
    {
        Startlist?.Remove(participation.Combination.Number);
        EmitChanged();
    }

    void AddEntry(Participation participation)
    {
        Startlist?.Add(participation);
        EmitChanged();
    }
}
