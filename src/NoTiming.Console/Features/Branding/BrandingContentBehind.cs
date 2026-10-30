using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Features.Console;

namespace NoTiming.Console.Features.Branding;

/// <summary>
/// The logos of an Event (#646, ADR-0012): the page the Main Operator puts its own in place of the federation's from, before the
/// Event ends. The address names the Event, and the Api says whether the person may (the capabilities of the Event), so a person who
/// may not is shown nothing.
/// </summary>
public class BrandingContentBehind : NStatefulComponent
{
    [Inject]
    IConsoleEvents Events { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    protected bool MayEdit => Events.Events.Any(x => x.Id == EventId && x.CanEditBranding);

    [Parameter]
    public Guid EventId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await Observe(Events);
    }

    protected void GoHome()
    {
        Navigator.NavigateTo(ConsoleRoutes.HOME);
    }
}
