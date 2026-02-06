using Not.Blazor.Components.Layout;
using Not.Exceptions;

namespace NTS.Judge.Blazor.Setup;

public class FormContent<T> : NMainContent
{
    protected T Model { get; set; } = default!;

    protected override void OnInitialized()
    {
        var model = GetRouteParameter<T>();
        GuardHelper.ThrowIfDefault(model);
        Model = model;
    }
}
