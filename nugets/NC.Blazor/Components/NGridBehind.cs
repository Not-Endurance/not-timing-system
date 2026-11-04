using MudBlazor;
using NC.Blazor.Components.Abstractions;

namespace NC.Blazor.Components;

public class NGridBehind : NComponentBase
{
    [Parameter]
    public int Spacing { get; set; } = 6;
}
