using System.Linq.Expressions;
using NC.Blazor.Components.Abstractions;

namespace NC.Blazor.Components.Input;

// A checkbox that shows its ChildContent only while it is checked. The consumer owns the value.
public abstract class NOptionalInputBehind : NComponentBase
{
    [Parameter]
    public bool Value { get; set; }

    [Parameter]
    public EventCallback<bool> ValueChanged { get; set; }

    [Parameter]
    public Expression<Func<bool>>? For { get; set; }

    [Parameter]
    public string? Label { get; set; }

    [Parameter]
    public string? HelperText { get; set; }
}
