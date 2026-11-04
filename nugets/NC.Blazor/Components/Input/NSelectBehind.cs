using System.Linq.Expressions;
using NC.Blazor.Components.Abstractions;
using NC.Structures;

namespace NC.Blazor.Components.Input;

public class NSelectBehind<T> : NComponentBase
{
    protected IEnumerable<NotListModel<T>> ResolvedItems { get; private set; } = [];

    protected Expression<Func<T>>? InputFor => For ?? ValueExpression;

    [Parameter]
    public IEnumerable<NotListModel<T>> Items { get; set; } = [];

    [Parameter]
    public T Value { get; set; } = default!;

    [Parameter]
    public EventCallback<T> ValueChanged { get; set; }

    [Parameter]
    public Expression<Func<T>>? ValueExpression { get; set; }

    [Parameter]
    public Expression<Func<T>>? For { get; set; }

    [Parameter]
    public string? Placeholder { get; set; }

    [Parameter]
    public string? Label { get; set; }

    protected override void OnParametersSet()
    {
        var type = typeof(T);
        if (type.IsEnum || (Nullable.GetUnderlyingType(type)?.IsEnum ?? false))
        {
            ResolvedItems = NotListModel.FromEnum<T>(type).ToList();
            return;
        }

        ResolvedItems = Items;
    }
}
