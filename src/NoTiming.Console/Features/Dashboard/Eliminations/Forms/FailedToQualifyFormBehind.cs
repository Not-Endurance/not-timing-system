using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Features.Console;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Objects;

namespace NoTiming.Console.Features.Dashboard.Eliminations.Forms;

public class FailedToQualifyFormBehind : NComponent
{
    [Inject]
    IConsoleDashboard Dashboard { get; set; } = default!;

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

    protected Task FailToQualifySafe()
    {
        return Dashboard.FailToQualify([.. Codes], Reason);
    }
}
