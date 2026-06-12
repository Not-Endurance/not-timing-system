using MudBlazor;
using Not.Blazor.Components.Abstractions;

namespace Not.Blazor.Components.Layout;

public class NDrawerBehind : NComponent
{
    protected Func<Task> CloseResponsiveDrawer => CloseResponsiveDrawerSafe;

    [Parameter]
    public bool Open { get; set; }

    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    [Parameter]
    public Breakpoint Breakpoint { get; set; } = MudBlazor.Breakpoint.Md;

    protected async Task SetOpen(bool open)
    {
        try
        {
            Open = open;
            await OpenChanged.InvokeAsync(open);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    async Task CloseResponsiveDrawerSafe()
    {
        try
        {
            var currentBreakpoint = await ViewportService.GetCurrentBreakpointAsync();
            if (!Open || !ShouldCloseAtBreakpoint(currentBreakpoint))
            {
                return;
            }

            await SetOpen(false);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    bool ShouldCloseAtBreakpoint(Breakpoint currentBreakpoint)
    {
        return Breakpoint switch
        {
            MudBlazor.Breakpoint.None => true,
            MudBlazor.Breakpoint.Always => false,
            _ => GetBreakpointOrder(currentBreakpoint) <= GetBreakpointOrder(Breakpoint),
        };
    }

    static int GetBreakpointOrder(Breakpoint breakpoint)
    {
        return breakpoint switch
        {
            MudBlazor.Breakpoint.Xs => 0,
            MudBlazor.Breakpoint.Sm => 1,
            MudBlazor.Breakpoint.Md => 2,
            MudBlazor.Breakpoint.Lg => 3,
            MudBlazor.Breakpoint.Xl => 4,
            MudBlazor.Breakpoint.Xxl => 5,
            MudBlazor.Breakpoint.SmAndDown => 1,
            MudBlazor.Breakpoint.MdAndDown => 2,
            MudBlazor.Breakpoint.LgAndDown => 3,
            MudBlazor.Breakpoint.XlAndDown => 4,
            MudBlazor.Breakpoint.SmAndUp => 1,
            MudBlazor.Breakpoint.MdAndUp => 2,
            MudBlazor.Breakpoint.LgAndUp => 3,
            MudBlazor.Breakpoint.XlAndUp => 4,
            _ => int.MaxValue,
        };
    }
}
