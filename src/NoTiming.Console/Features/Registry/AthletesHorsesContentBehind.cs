using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Features.Console;

namespace NoTiming.Console.Features.Registry;

/// <summary>
/// The Athletes and the Horses of the Tenant's registry (#646, ADR-0010): each is registered once and every Combination that
/// uses it carries its id. Anybody of the Tenant reads them; whoever the Api lets write them (a Tenant Root, or the Main
/// Operator of an Event that is not over) changes them.
/// </summary>
public class AthletesHorsesContentBehind : NStatefulComponent
{
    [Inject]
    IConsoleEvents Events { get; set; } = default!;

    [Inject]
    NavigationManager Pages { get; set; } = default!;

    protected bool ReadOnly => !Events.CanEditRegistry;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Events);
    }

    protected void GoHome()
    {
        Pages.NavigateTo(ConsoleRoutes.HOME);
    }
}
