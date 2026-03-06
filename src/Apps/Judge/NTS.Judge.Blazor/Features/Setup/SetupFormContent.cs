using Not.Blazor.Components;
using Not.Exceptions;

namespace NTS.Judge.Blazor.Features.Setup;

public class SetupFormContent<T> : NContentBehind
{
    protected T Model { get; set; } = default!;

    protected override void OnInitialized()
    {
        try
        {
            var model = GetRouteParameter<T>();
            GuardHelper.ThrowIfDefault(model);
            Model = model;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
