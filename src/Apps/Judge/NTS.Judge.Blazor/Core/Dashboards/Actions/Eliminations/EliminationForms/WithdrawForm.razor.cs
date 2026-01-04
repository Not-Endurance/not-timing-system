using NTS.Domain.Core.Aggregates.Participations;
using NTS.Judge.Blazor.Core.Dashboards.Actions.Eliminations.EliminationForms.Shared;

namespace NTS.Judge.Blazor.Core.Dashboards.Actions.Eliminations.EliminationForms;

public partial class WithdrawForm : EliminationForm
{
    [Parameter]
    public Withdrawn? Withdrawn { get; set; } = default!;

    internal override async Task Eliminate()
    {
        await Eliminations.Withdraw();
    }
}
