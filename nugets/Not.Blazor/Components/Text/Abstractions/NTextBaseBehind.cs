using MudBlazor;

namespace Not.Blazor.Components.Text.Abstractions;

public class NTextBaseBehind : MudText
{
    protected string AbsoluteCenterStyle =>
        IsAbsoluteCenter //TODO: fix naming rule here should be_absoluteCenterStyle
            ? "position: absolute; left: 0; width: 100%"
            : "";

    [Parameter, EditorRequired]
    public string Content { get; set; } = default!; //TODO: Change to ChildContent RenderFragment

    [Parameter]
    public bool IsAbsoluteCenter { get; set; }
}
