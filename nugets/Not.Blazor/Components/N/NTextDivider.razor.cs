namespace Not.Blazor.Components;

public partial class NTextDivider
{
    [Parameter, EditorRequired]
    public string Text { get; set; } = default!;
}
