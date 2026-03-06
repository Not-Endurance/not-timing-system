using Not.Blazor.Components.Abstractions;

namespace Not.Blazor.Components.Tables;

public class NTdBehind<T> : NComponent
{
    [Parameter]
    public T? Value { get; set; }

    [Parameter]
    public string? TextStyle { get; set; }

    [Parameter]
    public string? CellStyle { get; set; }
}
