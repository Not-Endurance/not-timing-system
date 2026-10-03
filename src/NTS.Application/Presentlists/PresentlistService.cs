using Not.Application.Behinds.Adapters;
using NTS.Contracts.Core;
using NTS.Contracts.Presentlists;
using NTS.Domain.Core.Objects.Presentlists;

namespace NTS.Application.Presentlists;

/// <summary>A view over the Participations of the store (ADR-0006): it rebuilds when the store changes.</summary>
public class PresentlistService : NStatefulService, IPresentlistService
{
    readonly IParticipationStore _store;

    public PresentlistService(IParticipationStore store)
    {
        _store = store;
        Observe(store, Rebuild);
    }

    public Presentlist Presentlist { get; private set; } = new([]);

    public IReadOnlyList<PresentlistEntry> Entries => Presentlist.Entries;

    protected override async Task<bool> InitializeState()
    {
        await _store.Load();
        Presentlist = Build();
        return Entries.Any();
    }

    public void Tick()
    {
        EmitChanged();
    }

    void Rebuild()
    {
        Presentlist = Build();
        EmitChanged();
    }

    Presentlist Build()
    {
        return new Presentlist(_store.Participations);
    }
}
