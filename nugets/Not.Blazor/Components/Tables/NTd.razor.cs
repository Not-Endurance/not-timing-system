namespace Not.Blazor.Components.Tables;

public partial class NTd<T>
{
    [Parameter]
    public T? Value { get; set; }

    [Parameter]
    public string? TextStyle { get; set; }

    [Parameter]
    public string? CellStyle { get; set; }
}
