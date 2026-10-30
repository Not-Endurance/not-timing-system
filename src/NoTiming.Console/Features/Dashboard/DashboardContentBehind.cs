using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Features.Console;
using NTS.Domain.Access;

namespace NoTiming.Console.Features.Dashboard;

/// <summary>
/// The dashboard of a Live Event (#646): the page the Main Operator runs it from. The address names the Event, and the Api
/// says who runs it (the capabilities of the Event), so a person who does not, and an Event that has not started, are shown
/// nothing: the Console offers no more than the Api would take. An Event that has ended is not shown the page either, and is
/// told so by the view (#631).
/// </summary>
public class DashboardContentBehind : NStatefulComponent
{
    [Inject]
    IConsoleEvents Events { get; set; } = default!;

    protected bool MayRun =>
        Events.Events.Any(x => x.Id == EventId && x is { IsMainOperator: true } && x.Stage != EventStage.Unstarted);

    [Parameter]
    public Guid EventId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await Observe(Events);
    }
}
