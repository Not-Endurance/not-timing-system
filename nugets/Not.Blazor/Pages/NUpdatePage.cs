using Not.Blazor.Components;
using Not.Blazor.Navigation;

namespace Not.Blazor.Pages;

public abstract class NUpdatePage : NBehind
{
    [Inject]
    protected ICrumbsNavigator Navigator { get; set; } = default!;
}
