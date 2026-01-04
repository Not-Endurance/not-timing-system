using Not.Notify;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Judge.Blazor.Core.Dashboards.Actions.Eliminations.EliminationForms.Shared;

namespace NTS.Judge.Blazor.Core.Dashboards.Actions.Eliminations.EliminationForms;

public partial class FinishNotRankedForm : EliminationForm
{
    string? _reason;

    [Parameter]
    public FinishedNotRanked? FinishedNotRanked { get; set; } = default!;

    protected override void OnParametersSet()
    {
        _reason = FinishedNotRanked?.Complement;
    }

    internal override async Task Eliminate()
    {
        if (_reason == null)
        {
            NotifyHelper.Warn("Reason is required");
            return;
        }

        await Eliminations.FinishNotRanked(_reason);
    }
}
