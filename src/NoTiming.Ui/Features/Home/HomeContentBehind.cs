using Not.Blazor.Components.Abstractions;
using NoTiming.Ui.Features.Socket;
using NTS.Contracts.Features.Access;
using NTS.Contracts.Socket;

namespace NoTiming.Ui.Features.Home;

/// <summary>
/// The first page of the app, which takes the person to the one that their access begins at once the app is connected. A
/// person who leaves it before then (a link of the drawer, such as the Console, opened at once) is not taken from where they
/// went to.
/// </summary>
public class HomeContentBehind : NStatefulComponent
{
    bool _hasRedirected;
    bool _left;

    [Inject]
    IWitnessAccessContext AccessState { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    [Inject]
    BlazorSocketService BlazorSocketService { get; set; } = default!;

    [Inject]
    INtsSocketService SocketService { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(AccessState);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _hasRedirected)
        {
            return;
        }

        _hasRedirected = true;
        await BlazorSocketService.EnsureConnected();
        if (_left)
        {
            return;
        }

        Navigator.NavigateTo(WitnessAccessPolicy.ResolveHomeRoute(AccessState.CanSendSnapshots, SocketService.Event?.Id));
    }

    public override void Dispose()
    {
        _left = true;
        base.Dispose();
    }
}
