using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Features.Console;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Objects;

namespace NoTiming.Console.Features.Dashboard.Eliminations;

/// <summary>
/// The elimination of the Participation the Console Operator works on (#646): the way it is out (withdrawn, retired, finished and
/// not ranked, disqualified, failed to qualify) and the reason there is for it, and the restoring of its qualification.
/// </summary>
public class EliminationsPanelBehind : NStatefulComponent
{
    string? _chosen;

    [Inject]
    IConsoleDashboard Dashboard { get; set; } = default!;

    protected Eliminated? Eliminated => Dashboard.Selected?.Eliminated;

    protected string? ToggleValue
    {
        get => _chosen ?? Eliminated?.Code;
        set => _chosen = value;
    }

    protected override async Task OnInitializedAsync()
    {
        await Observe(Dashboard);
    }

    protected override void OnBeforeRender()
    {
        _chosen = null; // what is shown follows the Participation again once the choice was drawn
    }
}
