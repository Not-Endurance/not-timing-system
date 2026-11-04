using MudBlazor;
using NC.Blazor.Components.Abstractions;

namespace NC.Blazor.Components;

public class NSkeletonBehind : NComponent
{
    [Parameter]
    public int Lines { get; set; } = 3;

    [Parameter]
    public SkeletonType Type { get; set; } = SkeletonType.Text;
}
