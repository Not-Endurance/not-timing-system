using NC.Blazor.Components.Abstractions;
using NoTiming.Domain.Core.Aggregates.Participations;
using NoTiming.Domain.Core.Aggregates.Participations.Objects;
using NoTiming.Judge.Contracts.Features.Core.Dashboard;

namespace NoTiming.Judge.Blazor.Features.Core.Dashboards.Components.Eliminations.Forms;

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
