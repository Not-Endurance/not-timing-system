using MudBlazor;

namespace Not.Blazor.Components.Tables;

public partial class NTable
{
    protected Typo Typography { get; set; } = Typo.caption;

    [Parameter, EditorRequired]
    public IEnumerable<object> Items { get; set; } = [];

    [Parameter, EditorRequired]
    public string[] Headings { get; set; } = default!;

    [Parameter]
    public string EmptyMessage { get; set; } = Empty_string;

    /// <summary>
    /// Splits the object into cell values. By default it uses <see cref="object.ToString"/> splits values by '|'
    /// </summary>
    [Parameter]
    public Func<object, IEnumerable<string>> Transform { get; set; } = x => x.ToString()?.Split('|') ?? [];

    [Parameter]
    public RenderFragment<object>? TrailingCell { get; set; }
}
