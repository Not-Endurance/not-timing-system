namespace Not.Blazor.Components;

public partial class NContent
{
    const int GRID_MAX_WIDTH = 12;

    bool _showOnlyMain;
    int MainXs =>
        Rightbar == null //TODO: fix naming rules for this case - should be _mainXs
            ? GRID_MAX_WIDTH
            : GRID_MAX_WIDTH - RightBarXS;

    [Parameter, EditorRequired]
    public string Title { get; set; } = default!;

    [Parameter]
    public RenderFragment? Main { get; set; } = default!;

    [Parameter]
    public RenderFragment? Main2 { get; set; } = default!;

    [Parameter]
    public RenderFragment? Rightbar { get; set; } = default!;

    [Parameter]
    public int RightBarXS { get; set; } = 3;

    [Parameter]
    public bool HasContent { get; set; } = true;

    [Parameter]
    public string? EmptyMessage { get; set; }

    [Parameter]
    public RenderFragment? EmptyContent { get; set; }

    protected override void OnInitialized()
    {
        PrintableComponent.OnToggle(ToggleVisibilityHandler);
    }

    async void ToggleVisibilityHandler()
    {
        _showOnlyMain = !_showOnlyMain;
        await InvokeRender();
    }
}
