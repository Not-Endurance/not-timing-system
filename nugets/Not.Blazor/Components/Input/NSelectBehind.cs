using MudBlazor;
using Not.Structures;

namespace Not.Blazor.Components;

public class NSelectBehind<T> : MudSelect<T>
{
    [Parameter]
    public new IEnumerable<NotListModel<T>> Items { get; set; } = default!;

    protected override void OnInitialized()
    {
        var type = typeof(T);
        if (type.IsEnum || (Nullable.GetUnderlyingType(type)?.IsEnum ?? false))
        {
            Items = NotListModel.FromEnum<T>(type).ToList();
        }

        base.OnInitialized();
    }
}
