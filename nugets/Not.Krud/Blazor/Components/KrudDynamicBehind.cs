using Microsoft.AspNetCore.Components;
using Not.Blazor.Components;
using Not.Blazor.CRUD.Forms.Components;
using Not.Exceptions;
using Not.Krud.Blazor.Components.Abstractions;

namespace Not.Krud.Blazor.Components;

public class KrudDynamicBehind<TModel, TForm> : NComponent
    where TForm : KrudFormContainer<TModel>
{
    protected DynamicComponent? _dynamicComponent;
    protected Dictionary<string, object> _parameters = [];

    [Parameter, EditorRequired]
    public TModel Model { get; set; } = default!;

    public TForm Instance
    {
        get
        {
            if (_dynamicComponent?.Instance == null)
            {
                throw GuardHelper.Exception($"Instance of '{typeof(TForm)}' is null");
            }
            return (TForm)_dynamicComponent.Instance;
        }
    }

    protected override void OnParametersSet()
    {
        if (Model == null)
        {
            return;
        }
        var key = nameof(NForm<TModel>.Model);
        if (_parameters.ContainsKey(key))
        {
            _parameters[key] = Model;
        }
        else
        {
            _parameters.Add(key, Model);
        }
    }
}
