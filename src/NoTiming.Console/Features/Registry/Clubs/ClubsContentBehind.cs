using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Features.Console;

namespace NoTiming.Console.Features.Registry.Clubs;

/// <summary>The Clubs of the Tenant's registry (#646, ADR-0010).</summary>
public class ClubsContentBehind : NStatefulComponent
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
