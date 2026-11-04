using NC.Blazor.Components.Abstractions;
using NC.Notify;
using NoTiming.Domain.Core.Aggregates.Participations.Objects;
using NoTiming.Judge.Contracts.Features.Core.Dashboard;

namespace NoTiming.Judge.Blazor.Features.Core.Dashboards.Components.Eliminations.Forms;

public abstract class FinishNotRankedFormBehind : NComponent
{
    [Inject]
    IEliminationService EliminationService { get; set; } = default!;

    [Inject]
    INotifier Notifier { get; set; } = default!;

    protected string? Reason { get; set; }

    [Parameter]
    public FinishedNotRanked? FinishedNotRanked { get; set; } = default!;

    protected override void OnParametersSet()
    {
        Reason = FinishedNotRanked?.Complement;
    }

    protected async Task FinishNotRankedSafe()
    {
        if (Reason == null)
        {
            Notifier.Warn(Reason_is_required_string);
            return;
        }

        await EliminationService.FinishNotRanked(Reason);
    }
}
