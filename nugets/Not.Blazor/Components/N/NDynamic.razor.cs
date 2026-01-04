using Not.Blazor.CRUD.Forms.Components;

namespace Not.Blazor.Components;

public partial class NDynamic<T, TForm>
    where TForm : NForm<T>
{
    DynamicComponent? _dynamicComponent;
    Dictionary<string, object> _parameters = [];

    [Parameter, EditorRequired]
    public T Model { get; set; } = default!;

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
        var key = nameof(NForm<T>.Model);
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
