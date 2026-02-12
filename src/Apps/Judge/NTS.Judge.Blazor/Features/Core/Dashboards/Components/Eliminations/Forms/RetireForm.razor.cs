using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Judge.Blazor.Features.Core.Dashboards.Components.Eliminations.Forms.Shared;

namespace NTS.Judge.Blazor.Features.Core.Dashboards.Components.Eliminations.Forms;

public partial class RetireForm : EliminationForm
{
    [Parameter]
    public Retired? Retired { get; set; }

    internal override async Task Eliminate()
    {
        await Eliminations.Retire();
    }
}
