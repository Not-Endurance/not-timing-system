using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Judge.Blazor.Features.Core.Dashboards.Components.Eliminations.Forms.Shared;

namespace NTS.Judge.Blazor.Features.Core.Dashboards.Components.Eliminations.Forms;

public partial class WithdrawForm : EliminationForm
{
    [Parameter]
    public Withdrawn? Withdrawn { get; set; } = default!;

    internal override async Task Eliminate()
    {
        await Eliminations.Withdraw();
    }
}
