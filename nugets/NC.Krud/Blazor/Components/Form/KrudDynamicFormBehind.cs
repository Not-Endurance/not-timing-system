using Microsoft.AspNetCore.Components;
using NC.Blazor.Components.Abstractions;
using NC.Exceptions;
using NC.Krud.Abstractions;
using NC.Krud.Blazor.Components.Abstractions;

namespace NC.Krud.Blazor.Components.Form;

public class KrudDynamicFormBehind<TShell, TModel> : NComponent
    where TShell : KrudShell<TModel>
    where TModel : IKrudFormModel, new()
{
    protected DynamicComponent? DynamicComponentRef { get; set; }
    protected Dictionary<string, object> ComponentParameters { get; set; } = [];

    [Parameter]
    public TModel Model { get; set; } = new();

    [Parameter, EditorRequired]
    public Func<TModel, Task> OnSubmit { get; set; } = default!;

    public TShell Instance
    {
        get
        {
            GuardHelper.ThrowIfDefault(DynamicComponentRef?.Instance);
            return (TShell)DynamicComponentRef.Instance;
        }
    }

    protected override void OnParametersSet()
    {
        OverwriteParameter(nameof(KrudShell<TModel>.OnSubmit), OnSubmit);
        if (Model == null)
        {
            return;
        }
        OverwriteParameter(nameof(KrudShell<TModel>.Model), Model);
    }

    void OverwriteParameter(string key, object value)
    {
        if (!ComponentParameters.TryAdd(key, value))
        {
            ComponentParameters[key] = value;
        }
    }
}
