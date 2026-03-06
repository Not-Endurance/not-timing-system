using Not.Blazor.Components.Abstractions;

namespace Not.Blazor.Components;

public class NDividerBehind : NComponent
{
    [Parameter]
    public string Text { get; set; } = default!;
}
