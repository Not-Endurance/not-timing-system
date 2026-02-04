using Microsoft.AspNetCore.Components;
using Not.Blazor.Components;
using Not.Exceptions;
using Not.Krud.Abstractions;
using Not.Krud.Blazor.Components.Abstractions;

namespace Not.Krud.Blazor.Components.Form;

public class KrudDynamicFormBehind<TForm, TModel> : NComponent
    where TForm : KrudFormShell<TModel>
    where TModel : IKrudFormModel, new()
{
    protected DynamicComponent? DynamicComponentRef { get; set; }
    protected Dictionary<string, object> ComponentParameters { get; set; } = [];

    [Parameter]
    public TModel Model { get; set; } = new();

    [Parameter, EditorRequired]
    public Func<TModel, Task> OnSubmit { get; set; } = default!;

    public TForm Instance
    {
        get
        {
            GuardHelper.ThrowIfDefault(DynamicComponentRef?.Instance);
            return (TForm)DynamicComponentRef.Instance;
        }
    }

    protected override void OnParametersSet()
    {
        OverwriteParameter(nameof(KrudFormShell<TModel>.OnSubmit), OnSubmit);
        if (Model == null)
        {
            return;
        }
        OverwriteParameter(nameof(KrudFormShell<TModel>.Model), Model);
    }

    void OverwriteParameter(string key, object value)
    {
        if (!ComponentParameters.TryAdd(key, value))
        {
            ComponentParameters[key] = value;
        }
    }
}
