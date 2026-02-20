using Not.Blazor.Components.Abstractions;
using Not.Blazor.Navigation;

namespace Not.Blazor.Pages;

public abstract class NUpdatePage : NComponent
{
    [Inject]
    protected ICrumbsNavigator Navigator { get; set; } = default!;
}
