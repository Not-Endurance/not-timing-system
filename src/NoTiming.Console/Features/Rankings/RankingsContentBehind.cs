using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Features.Console;

namespace NoTiming.Console.Features.Rankings;

/// <summary>
/// The Rankings and Results of an Event, live or historic (#646): the page opens for the people the Console lists the Event
/// to, which is the Api's word on who sees it, and shows nothing to anybody else.
/// </summary>
public class RankingsContentBehind : NStatefulComponent
{
    [Inject]
    IConsoleEvents Events { get; set; } = default!;

    protected bool IsListed => Events.Events.Any(x => x.Id == EventId);

    [Parameter]
    public Guid EventId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await Observe(Events);
    }
}
