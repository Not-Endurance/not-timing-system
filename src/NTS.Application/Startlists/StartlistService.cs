using Not.Application.Behinds.Adapters;
using NTS.Contracts.Core;
using NTS.Contracts.Startlists;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Application.Startlists;

/// <summary>A view over the Participations of the store (ADR-0006): it rebuilds when the store changes.</summary>
public class StartlistService : NStatefulService, IStartUpcoming, IStartHistory
{
    readonly IParticipationStore _store;

    public StartlistService(IParticipationStore store)
    {
        _store = store;
        Observe(store, Rebuild);
    }

    public Startlist Startlist { get; private set; } = new([]);

    public IReadOnlyList<Starter> Upcoming => Startlist.Upcoming;

    public IReadOnlyList<Starter> History => Startlist.History;
    public IReadOnlyDictionary<int, IReadOnlyList<Starter>> HistoryByStage => Startlist.HistoryByStage;

    protected override async Task<bool> InitializeState()
    {
        await _store.Load();
        Startlist = Build();
        return Startlist.History.Any() || Startlist.Upcoming.Any();
    }

    public void Tick()
    {
        Rebuild();
    }

    void Rebuild()
    {
        Startlist = Build();
        EmitChanged();
    }

    Startlist Build()
    {
        return new Startlist(new UniqueParticipations(_store.Participations));
    }
}
