using Not.Blazor.Components.Abstractions;
using Not.Notify;
using NTS.Contracts.Features.Console;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Objects;

namespace NoTiming.Console.Features.Dashboard.Eliminations.Forms;

public class DisqualifyFormBehind : NComponent
{
    [Inject]
    IConsoleDashboard Dashboard { get; set; } = default!;

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
            Reason = Disqualified.Complement;
        }
    }

    protected Task DisqualifySafe()
    {
        if (string.IsNullOrWhiteSpace(Reason) && !Codes.Any())
        {
            Notifier.Warn(Reason_is_required_string);
            return Task.CompletedTask;
        }

        return Dashboard.Disqualify([.. Codes], Reason);
    }
}
