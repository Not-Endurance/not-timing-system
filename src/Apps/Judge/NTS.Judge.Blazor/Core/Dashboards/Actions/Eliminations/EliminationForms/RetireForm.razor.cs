using NTS.Domain.Core.Aggregates.Participations;
using NTS.Judge.Blazor.Core.Dashboards.Actions.Eliminations.EliminationForms.Shared;

namespace NTS.Judge.Blazor.Core.Dashboards.Actions.Eliminations.EliminationForms;

public partial class RetireForm : EliminationForm
{
    [Parameter]
    public Retired? Retired { get; set; }

    internal override async Task Eliminate()
    {
        await Eliminations.Retire();
    }
}
