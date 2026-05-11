using Not.Blazor.Components;
using Not.Exceptions;
using Not.Krud.Abstractions;
using Not.Krud.Models;

namespace NTS.Judge.Blazor.Features.Setup;

public class SetupFormContent<T> : NContentBehind
    where T : IKrudFormModel, new()
{
    protected T Model { get; set; } = default!;
    protected bool ReadOnly { get; private set; }

    protected override void OnInitialized()
    {
        try
        {
            // TODO: Unify route parameter flow - have a main generid Data and Metadata dictionary
            var routeParameter = GetRouteParameter<KrudFormRouteParameter<T>>();
            var model = routeParameter.Model;
            GuardHelper.ThrowIfDefault(model);
            Model = model;
            ReadOnly = routeParameter.ReadOnly;
        }
        catch (GuardException)
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
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
