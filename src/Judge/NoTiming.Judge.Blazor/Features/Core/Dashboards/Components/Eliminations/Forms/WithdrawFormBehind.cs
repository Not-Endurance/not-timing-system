using NC.Blazor.Components.Abstractions;
using NoTiming.Domain.Core.Aggregates.Participations.Objects;
using NoTiming.Judge.Contracts.Features.Core.Dashboard;

namespace NoTiming.Judge.Blazor.Features.Core.Dashboards.Components.Eliminations.Forms;

public abstract class WithdrawFormBehind : NComponent
{
    [Inject]
    IEliminationService EliminationService { get; set; } = default!;

    [Parameter]
    public Withdrawn? Withdrawn { get; set; } = default!;

    protected async Task WithdrawSafe()
    {
        await EliminationService.Withdraw();
    }
}
