using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using Not.Blazor.Components;
using Not.Blazor.CRUD.Forms.Validation;
using Not.Notify;

namespace Not.Blazor.CRUD.Forms.Components;

public abstract class NForm<T> : NComponent
{
    public abstract void RegisterValidationInjectors();

    /// <summary>
    /// Contains refs to the actual field components, necessary in order to render Mud validation messages from the DomainException
    /// See discussion about Cascading EditContext and custom validator: https://github.com/MudBlazor/MudBlazor/issues/8175#issuecomment-3837793584
    /// Example: https://github.com/MudBlazor/MudBlazor/issues/7658
    /// </summary>
    protected Dictionary<string, List<MudValidationInjector>> ValidationInjectors { get; set; } = [];

    [Parameter]
    public T Model { get; set; } = default!;

    protected override void OnInitialized()
    {
        RegisterValidationInjectors();
    }

    protected void RegisterInjector<TInput>(string field, Func<MudBaseInput<TInput>> mudInputInstanceGetter)
    {
        var injector = MudValidationInjector.Create(mudInputInstanceGetter);
        AddInjector(field, injector);
    }

    protected void RegisterInjector<TInput>(string field, Func<IMudBaseInputWrapper<TInput>> mudInputWrapper)
    {
        var injector = MudValidationInjector.Create(mudInputWrapper);
        AddInjector(field, injector);
    }

    protected void RegisterInjector<TInput>(string field, Func<MudPicker<TInput>> mudInputInstanceGetter)
    {
        var injector = MudValidationInjector.Create(mudInputInstanceGetter);
        AddInjector(field, injector);
    }

    protected void RegisterInjector(string field, Func<NSwitch> mudInputInstanceGetter)
    {
        var injector = MudValidationInjector.Create<bool, bool>(mudInputInstanceGetter);
        AddInjector(field, injector);
    }

    public async Task AddValidationError(string? field, string message)
    {
        if (field == null)
        {
            NotifyHelper.Warn(message);
            return;
        }
        if (!ValidationInjectors.TryGetValue(field, out var injectors))
        {
            NotifyHelper.Warn(message);
            var error = GuardHelper.Exception($"'{typeof(T)}' form does not have injector for field '{field}'");
            NotifyHelper.Error(error);
            return;
        }

        foreach (var injector in injectors)
        {
            injector.Inject(message);
        }
        await InvokeRender();
    }

    void AddInjector(string field, MudValidationInjector injector)
    {
        if (!ValidationInjectors.TryAdd(field, [injector]))
        {
            ValidationInjectors[field].Add(injector);
        }
    }
}
