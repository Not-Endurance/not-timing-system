using Microsoft.AspNetCore.Components;
using Not.Blazor.Components.Abstractions;
using NTS.Application.Contracts.Arrivelists;
using NTS.Domain.Core.Objects.Arrivelists;
using NTS.Domain.Objects;

namespace NTS.Blazor.Components.Arrivelist;

public class ArrivelistTableBehind : NStatefulComponent
{
    [Inject]
    protected IArrivelistService Service { get; set; } = default!;

    /// <summary>
    /// Which estimate model the table shows. Component-local and reset on navigation - this is a
    /// diagnostic control while the two models are being compared, not a persisted preference.
    /// </summary>
    protected ArrivelistEstimateMode Mode { get; set; } = ArrivelistEstimateMode.Fei;

    /// <summary>
    /// The domain orders entries by the FEI sort estimate. Only the Regional selection re-sorts - FEI
    /// and Both both keep the domain's order, so that switching to Both to compare the two models does
    /// not also shift the rows. Under Regional the rendered order deliberately differs from the
    /// domain's entry order: the selected mode is presentation state and is not passed into the
    /// Arrivelist.
    /// </summary>
    protected IReadOnlyList<ArrivelistEntry> Entries =>
        Mode == ArrivelistEstimateMode.Regional ? OrderByRegional(Service.Entries) : Service.Entries;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }

    protected void Tick()
    {
        Service.Tick();
    }

    static IReadOnlyList<ArrivelistEntry> OrderByRegional(IReadOnlyList<ArrivelistEntry> entries)
    {
        return entries
            .OrderBy(x => RegionalSortEstimate(x) == null)
            .ThenBy(RegionalSortEstimate)
            .ThenBy(x => x.Number)
            .ToList();
    }

    /// <summary>
    /// Mirrors the domain's own first-non-null fallback. That helper is internal to the domain
    /// assembly and is deliberately not widened for a presentation-only sort - keep the two in step.
    /// </summary>
    static Timestamp? RegionalSortEstimate(ArrivelistEntry entry)
    {
        return entry.Regional.Fast ?? entry.Regional.Average ?? entry.Regional.Slow;
    }
}
