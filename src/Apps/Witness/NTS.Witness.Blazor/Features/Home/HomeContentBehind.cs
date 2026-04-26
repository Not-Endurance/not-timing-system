using Not.Blazor.Components.Abstractions;
using NTS.Witness.Contracts.Features.Access;

namespace NTS.Witness.Blazor.Features.Home;

public class HomeContentBehind : NStatefulComponent
{
    bool _hasRedirected;

    [Inject]
    IWitnessAccessContext AccessState { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(AccessState);
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _hasRedirected)
        {
            return Task.CompletedTask;
        }

        _hasRedirected = true;
        Navigator.NavigateTo(WitnessAccessPolicy.ResolveHomeRoute(AccessState.AccessLevel));
        return Task.CompletedTask;
    }
}
