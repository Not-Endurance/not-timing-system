using System.Globalization;
using MudBlazor;
using Not.Blazor.Components.Abstractions;

namespace Not.Blazor.Components.Tables;

public class NTableBehind : NComponentBase
{
    protected Typo Typography { get; set; } = Typo.caption;
    protected bool HasTrailingCell => TrailingCell is not null;
    protected int TotalColumnCount => Headings.Length + (HasTrailingCell ? 1 : 0);
    protected string StretchColumnStyle =>
        Headings.Length == 0
            ? string.Empty
            : $"width: {(100d / Headings.Length).ToString("0.###############", CultureInfo.InvariantCulture)}%;";
    protected string TrailingColumnStyle => "width: 1%;";
    protected string TrailingCellStyle => "width: 1%; white-space: nowrap;";

    [Parameter]
    public bool FixedHeader { get; set; }

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
