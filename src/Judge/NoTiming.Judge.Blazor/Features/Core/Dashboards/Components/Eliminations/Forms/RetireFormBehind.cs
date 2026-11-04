using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Judge.Contracts.Features.Core.Dashboard;

namespace NTS.Judge.Blazor.Features.Core.Dashboards.Components.Eliminations.Forms;

public abstract class RetireFormBehind : NComponent
{
    [Inject]
    IEliminationService EliminationService { get; set; } = default!;

    [Parameter]
    public Retired? Retired { get; set; }

    protected async Task RetireSafe()
    {
        await EliminationService.Retire();
    }
}
