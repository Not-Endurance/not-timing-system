using MudBlazor;

namespace NTS.Judge.Blazor.Features.Core.Rankings.Ranklists;

public class RanklistTableFrameBehind : MudGrid
{
    [Parameter]
    public RenderFragment One { get; set; } = default!;

    [Parameter]
    public RenderFragment Two { get; set; } = default!;

    [Parameter]
    public RenderFragment Three { get; set; } = default!;

    [Parameter]
    public RenderFragment Four { get; set; } = default!;

    [Parameter]
    public RenderFragment Five { get; set; } = default!;

    [Parameter]
    public RenderFragment Six { get; set; } = default!;

    [Parameter]
    public RenderFragment Seven { get; set; } = default!;

    [Parameter]
    public RenderFragment Eight { get; set; } = default!;
}
