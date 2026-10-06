using Not.Application.CRUD.Ports;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Access;
using NTS.Domain.Access;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Features.Core.EventViews;

/// <summary>
/// A Live Event as the connection follows it (#630, ADR-0007): it wraps the store the live views already share, which the
/// change notification keeps current (ADR-0006), and what the Api says the person may do about the Event. It holds no
/// Participation of its own, and tells whoever shows it when either of the two it wraps changes.
/// </summary>
public sealed class LiveEventView : EventView
{
    readonly IParticipationStore _store;
    readonly IWitnessAccessContext _access;

    public LiveEventView(
        EventInformation eventInformation,
        IParticipationStore store,
        IWitnessAccessContext access,
        IRepository<Ranking> rankings,
        IRepository<Official> officials
    )
        : base(eventInformation, EventStage.Live, rankings, officials)
    {
        _store = store;
        _access = access;
        Observe(store, EmitChanged);
        Observe(access, EmitChanged);
    }

    protected override bool MayWrite => WitnessAccessPolicy.CanViewSnapshots(_access.AccessLevel);

    public override IReadOnlyList<Participation> Participations => _store.Participations;

    protected override async Task<bool> InitializeState()
    {
        await _store.Load();
        await _access.Load();
        return await base.InitializeState();
    }
}
