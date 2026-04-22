using System.Globalization;
using MudBlazor;
using Not.Blazor.Components.Abstractions;

namespace Not.Blazor.Components.Tables;

public class NTableBehind : NComponentBase
{
    protected Typo Typography { get; set; } = Typo.caption;
    protected bool HasLeadingCell => LeadingCell is not null;
    protected bool HasTrailingCell => TrailingCell is not null;
    protected int TotalColumnCount => Headings.Length + (HasLeadingCell ? 1 : 0) + (HasTrailingCell ? 1 : 0);
    protected string StretchColumnStyle =>
        Headings.Length == 0
            ? string.Empty
            : $"width: {(100d / Headings.Length).ToString("0.###############", CultureInfo.InvariantCulture)}%;";
    protected string LeadingColumnStyle => "width: 1%; white-space: nowrap;";
    protected string LeadingCellStyle => "width: 1%; white-space: nowrap;";
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

    [Parameter]
    public string? LeadingHeading { get; set; }

    [Parameter]
    public RenderFragment<object>? LeadingCell { get; set; }

    [Parameter]
    public bool LeadingCellAsHeader { get; set; }

    [Parameter]
    public RenderFragment<int>? HeadingCell { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? UserAttributes { get; set; }
}
