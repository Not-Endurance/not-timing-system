using MudBlazor;

namespace Not.Blazor.Components;

public partial class NSimpleTable<T>
{
    Typo _typography = Typo.caption;

    [Parameter, EditorRequired]
    public IEnumerable<T> Items { get; set; } = [];

    [Parameter, EditorRequired]
    public string[] Headings { get; set; } = default!;

    [Parameter]
    public string EmptyMessage { get; set; } = "This table is still empty";

    [Parameter]
    public string Height { get; set; } = default!;

    [Parameter]
    public RenderFragment<T> CustomCell { get; set; } = default!;
}
