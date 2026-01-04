namespace NTS.Judge.Blazor.Core.Rankings.Ranklists;

public partial class RanklistTableFrame
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
