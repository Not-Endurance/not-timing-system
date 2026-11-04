using NC.Blazor.Components.Abstractions;
using NC.Notify;
using NoTiming.Domain.Core.Aggregates.Participations;
using NoTiming.Domain.Core.Aggregates.Participations.Objects;
using NoTiming.Judge.Contracts.Features.Core.Dashboard;

namespace NoTiming.Judge.Blazor.Features.Core.Dashboards.Components.Eliminations.Forms;

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
            Notifier.Warn(Reason_is_required_string);
            return;
        }
        var dqCodes = Codes.ToArray();
        await EliminationService.Disqualify(dqCodes, Reason);
    }
}
