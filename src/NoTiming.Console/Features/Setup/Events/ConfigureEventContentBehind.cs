using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Features.Console;

namespace NoTiming.Console.Features.Setup.Events;

/// <summary>
/// The Setup of an Event, opened by its Console Operator (#646): the form of the Event and, under it, the lists of what the
/// Event is made of. The address names the Event, so the page is the same after a reload, and it is read from the Api each
/// time it is opened. Whoever else opens the address finds nothing there: the Api says who runs the Event, and the Console
/// shows only that.
/// </summary>
public class ConfigureEventContentBehind : NStatefulComponent
{
    [Inject]
    IConsoleSetup Setup { get; set; } = default!;

    [Inject]
    NavigationManager Pages { get; set; } = default!;

    protected OpenedSetup? Opened { get; private set; }

    /// <summary>Changes each time the Setup is read, so that the lists under the form read what they hold again.</summary>
    protected int Reading { get; private set; }

    protected bool ReadOnly => Opened?.CanEdit != true;
    protected string CompetitionRoute => Routes.Of(ConsoleRoutes.COMPETITION_PAGE, EventId);
    protected string OfficialRoute => Routes.Of(ConsoleRoutes.OFFICIAL_PAGE, EventId);
    protected string LoopRoute => Routes.Of(ConsoleRoutes.LOOP_PAGE, EventId);
    protected string CombinationRoute => Routes.Of(ConsoleRoutes.COMBINATION_PAGE, EventId);

    [Parameter]
    public Guid EventId { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        await Open();
    }

    /// <summary>The form of the Event was saved: the Setup is read again, as what is stored is what the lists should show.</summary>
    protected async Task Reopen(ConfigureEventFormModel _)
    {
        await Open();
        await InvokeRender();
    }

    protected void GoHome()
    {
        Pages.NavigateTo(ConsoleRoutes.HOME);
    }

    async Task Open()
    {
        try
        {
            Opened = await Setup.Open(EventId);
            Reading++;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
