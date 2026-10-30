using Not.Blazor.Components.Abstractions;
using Not.Notify;
using NTS.Contracts.Features.Console;
using NTS.Domain.Core.Aggregates.Participations.Objects;

namespace NoTiming.Console.Features.Dashboard.Eliminations.Forms;

public class FinishNotRankedFormBehind : NComponent
{
    [Inject]
    IConsoleDashboard Dashboard { get; set; } = default!;

    [Inject]
    INotifier Notifier { get; set; } = default!;

    protected string? Reason { get; set; }

    [Parameter]
    public FinishedNotRanked? FinishedNotRanked { get; set; }

    protected override void OnParametersSet()
    {
        Reason = FinishedNotRanked?.Complement;
    }

    protected Task FinishNotRankedSafe()
    {
        if (string.IsNullOrWhiteSpace(Reason))
        {
            Notifier.Warn(Reason_is_required_string);
            return Task.CompletedTask;
        }

        return Dashboard.FinishNotRanked(Reason);
    }
}
