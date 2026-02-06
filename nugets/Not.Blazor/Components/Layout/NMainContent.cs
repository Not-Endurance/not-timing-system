using Not.Blazor.Navigation;

namespace Not.Blazor.Components.Layout;

public class NMainContent : NComponent
{

    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    protected T GetRouteParameter<T>()
    {
        return Navigator.ConsumeParameter<T>();
    }
}
