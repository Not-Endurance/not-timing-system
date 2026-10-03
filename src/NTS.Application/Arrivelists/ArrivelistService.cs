using Not.Application.Behinds.Adapters;
using NTS.Contracts.Arrivelists;
using NTS.Contracts.Core;
using NTS.Domain.Core.Objects.Arrivelists;

namespace NTS.Application.Arrivelists;

/// <summary>A view over the Participations of the store (ADR-0006): it rebuilds when the store changes.</summary>
public class ArrivelistService : NStatefulService, IArrivelistService
{
    readonly IParticipationStore _store;

    public ArrivelistService(IParticipationStore store)
    {
        _store = store;
        Observe(store, Rebuild);
    }

    public Arrivelist Arrivelist { get; private set; } = new([]);
    public IReadOnlyList<ArrivelistEntry> Entries => Arrivelist.Entries;

    protected override async Task<bool> InitializeState()
    {
        await _store.Load();
        Arrivelist = Build();
        return Entries.Any();
    }

    public void Tick()
    {
        Rebuild();
    }

    void Rebuild()
    {
        Arrivelist = Build();
        EmitChanged();
    }

    Arrivelist Build()
    {
        return new Arrivelist(_store.Participations);
    }
}
