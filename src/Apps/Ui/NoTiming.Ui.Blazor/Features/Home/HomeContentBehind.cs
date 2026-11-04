using Not.Blazor.Components.Abstractions;
using NoTiming.Ui.Blazor.Features.Socket;
using NoTiming.Ui.Contracts.Features.Access;

namespace NoTiming.Ui.Blazor.Features.Home;

public class HomeContentBehind : NStatefulComponent
{
    bool _hasRedirected;

    [Inject]
    IWitnessAccessContext AccessState { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    [Inject]
    BlazorSocketService BlazorSocketService { get; set; } = default!;

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
        Navigator.NavigateTo(WitnessAccessPolicy.ResolveHomeRoute(AccessState.AccessLevel));
    }
}
