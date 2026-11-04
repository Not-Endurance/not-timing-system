using NC.Blazor.Components.Abstractions;
using NoTiming.Domain.Core.Aggregates.Participations.Objects;
using NoTiming.Judge.Contracts.Features.Core.Dashboard;

namespace NoTiming.Judge.Blazor.Features.Core.Dashboards.Components.Eliminations.Forms;

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
