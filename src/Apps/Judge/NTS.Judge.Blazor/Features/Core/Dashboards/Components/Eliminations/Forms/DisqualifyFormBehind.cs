using Not.Blazor.Components.Abstractions;
using Not.Notify;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Judge.Features.Core.Dashboard;

namespace NTS.Judge.Blazor.Features.Core.Dashboards.Components.Eliminations.Forms;

public abstract class DisqualifyFormBehind : NComponent
{
    [Inject]
    IEliminationService EliminationService { get; set; } = default!;

    [Inject]
    INotifier Notifier { get; set; } = default!;

    protected string? Reason { get; set; }

    protected IEnumerable<DisqualifyCode> Codes { get; set; } = [];

    [Parameter]
    public Disqualified? Disqualified { get; set; }

    protected override void OnParametersSet()
    {
        if (Disqualified != null)
        {
            Codes = Disqualified.DqCodes.ToList();
            Reason = Disqualified?.Complement;
        }
    }

    protected async Task DisqualifySafe()
    {
        if (Reason == null && !Codes.Any())
        {
            Notifier.Warn("Reason is required");
            return;
        }
        var dqCodes = Codes.ToArray();
        await EliminationService.Disqualify(dqCodes, Reason);
    }
}
