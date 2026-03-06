using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Judge.Features.Core.Dashboard;

namespace NTS.Judge.Blazor.Features.Core.Dashboards.Components.Eliminations.Forms;

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
