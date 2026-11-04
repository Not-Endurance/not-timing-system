using MudBlazor;
using Not.Blazor.Components.Abstractions;

namespace Not.Blazor.Components.Text.Abstractions;

public class NTextBaseBehind : NComponentBase
{
    protected string AbsoluteCenterStyle =>
        IsAbsoluteCenter //TODO: fix naming rule here should be_absoluteCenterStyle
            ? "position: absolute; left: 0; width: 100%"
            : "";

    [Parameter, EditorRequired]
    public string Content { get; set; } = default!; //TODO: Change to ChildContent RenderFragment

    [Parameter]
    public bool IsAbsoluteCenter { get; set; }

    [Parameter]
    public Typo Typo { get; set; } = Typo.body1;

    [Parameter]
    public Align Align { get; set; }

    [Parameter]
    public Color Color { get; set; } = Color.Inherit;
}
