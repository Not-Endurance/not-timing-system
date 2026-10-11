using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Features.Console;
using NTS.Domain.Access;

namespace NoTiming.Console.Features.Startlist;

/// <summary>
/// The Startlist of a Live Event as a sheet to print (#646): the page the Main Operator hands it out from. The address names
/// the Event, and the Api says who runs it (the capabilities of the Event), so a person who does not, and an Event that is not
/// Live, are shown nothing: the Console offers no more than the Api would take.
/// </summary>
public class StartlistContentBehind : NStatefulComponent
{
    [Inject]
    IConsoleEvents Events { get; set; } = default!;

    protected bool MayRun =>
        Events.Events.Any(x => x.Id == EventId && x is { IsMainOperator: true, Stage: EventStage.Live });

    [Parameter]
    public Guid EventId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await Observe(Events);
    }
}
