using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Judge.Blazor.Features.Core.Dashboards.Components.Eliminations.Forms.Shared;

namespace NTS.Judge.Blazor.Features.Core.Dashboards.Components.Eliminations.Forms;

public partial class FailedToQualifyForm : EliminationForm
{
    string? _reason;
    IEnumerable<FailToQualifyCode> Codes { get; set; } = [];

    [Parameter]
    public FailedToQualify? FailedToQualify { get; set; }

    protected override void OnParametersSet()
    {
        if (FailedToQualify != null)
        {
            Codes = FailedToQualify.FtqCodes.ToList();
            _reason = FailedToQualify.Complement;
        }
    }

    internal override async Task Eliminate()
    {
        var ftqCodes = Codes.ToArray();
        await Eliminations.FailToQualify(ftqCodes, _reason);
    }
}
