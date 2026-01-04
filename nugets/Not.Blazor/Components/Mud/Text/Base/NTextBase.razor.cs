using Not.Localization;

namespace Not.Blazor.Components;

public partial class NTextBase
{
    string AbsoluteCenterStyle =>
        IsAbsoluteCenter //TODO: fix naming rule here should be_absoluteCenterStyle
            ? "position: absolute; left: 0; width: 100%"
            : "";

    [Parameter, EditorRequired]
    public string Content { get; set; } = default!; //TODO: Change to ChildContent RenderFragment

    [Parameter]
    public bool IsAbsoluteCenter { get; set; }
}
