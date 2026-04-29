using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Judge.Contracts.Features.Core.Dashboard;

namespace NTS.Judge.Blazor.Features.Core.Dashboards.Components.Eliminations.Forms;

public abstract class FailedToQualifyFormBehind : NComponent
{
    [Inject]
    IEliminationService EliminationService { get; set; } = default!;
    protected IEnumerable<FailToQualifyCode> Codes { get; set; } = [];

    protected string? Reason { get; set; }

    [Parameter]
    public FailedToQualify? FailedToQualify { get; set; }

    protected override void OnParametersSet()
    {
        if (FailedToQualify != null)
        {
            Codes = FailedToQualify.FtqCodes.ToList();
            Reason = FailedToQualify.Complement;
        }
    }

    protected async Task FailToQualifySafe()
    {
        var ftqCodes = Codes.ToArray();
        await EliminationService.FailToQualify(ftqCodes, Reason);
    }
}
