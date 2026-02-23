using Not.Blazor.Navigation;

namespace Not.Blazor.Components.Layout;

public class NContentBehind : NComponent
{
    const int GRID_MAX_WIDTH = 12;

    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    protected bool ShowOnlyMain { get; private set; }
    protected int MainXs => Rightbar == null ? GRID_MAX_WIDTH : GRID_MAX_WIDTH - RightBarXS;

    [Parameter, EditorRequired]
    public string Title { get; set; } = default!;

    /// <summary>
    /// If <see cref="true"/> renders an empty message instead of the contents to prevent exceptions
    /// </summary>
    [Parameter]
    public bool IsEmpty { get; set; } = false;

    [Parameter]
    public bool IsLoading { get; set; }

    [Parameter]
    public RenderFragment? Main { get; set; } = default!;

    [Parameter]
    public RenderFragment? Main2 { get; set; } = default!;

    [Parameter]
    public RenderFragment? Rightbar { get; set; } = default!;

    [Parameter]
    public int RightBarXS { get; set; } = 3;

    [Parameter]
    public string? EmptyMessage { get; set; } = Empty_string;

    [Parameter]
    public RenderFragment? EmptyContent { get; set; }

    protected T GetRouteParameter<T>()
    {
        return Navigator.ConsumeParameter<T>();
    }

    protected override void OnInitialized()
    {
        PrintableComponent.OnToggle(ToggleVisibilityHandler);
    }

    async void ToggleVisibilityHandler()
    {
        ShowOnlyMain = !ShowOnlyMain;
        await InvokeRender();
    }
}
