using System.Linq.Expressions;
using MudBlazor;
using Not.Blazor.Components.Abstractions;

namespace Not.Blazor.Components.Input;

public abstract class NSwitchBehind : NComponentBase
{
    public NSwitchBehind()
    {
        Color = Color.Primary;
    }

    protected Expression<Func<bool>>? InputFor => For ?? ValueExpression;

    [Parameter]
    public bool Value { get; set; }

    [Parameter]
    public EventCallback<bool> ValueChanged { get; set; }

    [Parameter]
    public Expression<Func<bool>>? ValueExpression { get; set; }

    [Parameter]
    public Expression<Func<bool>>? For { get; set; }

    [Parameter]
    public string? Label { get; set; }

    [Parameter]
    public Color Color { get; set; }
}
