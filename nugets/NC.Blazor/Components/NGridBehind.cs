using MudBlazor;
using Not.Blazor.Components.Abstractions;

namespace Not.Blazor.Components;

public class NGridBehind : NComponentBase
{
    [Parameter]
    public int Spacing { get; set; } = 6;
}
