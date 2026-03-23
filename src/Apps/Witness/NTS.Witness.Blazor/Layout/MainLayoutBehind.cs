using Not.Application.Authentication.Abstractions;
using Not.Blazor.Navigation.Abstractions;
using Not.Safe;
using Not.Startup;
using NTS.Application.Socket;
using static NTS.Witness.Blazor.Routes;

namespace NTS.Witness.Blazor.Layout;

public class MainLayoutBehind : LayoutComponentBase
{
    bool _hasStarted;

    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    [Inject]
    INAuthentication Authentication { get; set; } = default!;

    [Inject]
    IEnumerable<IStartupInitializer> Initializers { get; set; } = default!;

    [Inject]
    IEnumerable<IStartupInitializerAsync> AsyncInitializers { get; set; } = default!;

    [Inject]
    INtsSocketService SocketService { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _hasStarted)
        {
            return;
        }

        _hasStarted = true;

        try
        {
            foreach (var initializer in Initializers)
            {
               initializer.RunAtStartup();
            }
            foreach (var initializer in AsyncInitializers)
            {
               await initializer.RunAtStartupAsync();
            }
        }
        catch (Exception ex)
        {
            SafeHelper.HandleException(ex);
        }
    }

    protected void HelpHandler()
    {
        Navigator.NavigateTo(EMERGENCY_CONTACTS_PAGE);
    }

    protected Task Signout()
    {
        Authentication.Signout();
        SocketService.Disconnect();
        return Task.CompletedTask;
    }
}
