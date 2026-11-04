using NC.Blazor.Components.Abstractions;

namespace NC.Blazor.Components;

public class NDividerBehind : NComponent
{
    [Parameter]
    public string Text { get; set; } = default!;
}
