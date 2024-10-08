using MudBlazor;
using Not.Blazor.Components;
using Not.Blazor.Forms;
using Not.Notifier;

namespace Not.Blazor.TM.Forms.Components;

public abstract class FormTM<T> : NotComponent, ICreateForm<T>, IUpdateForm<T>
{
    /// <summary>
    /// Contains refs to the actual field components, necessary in order to render Mud validation messages from the DomainException
    /// This is a workaround until: https://github.com/eSolutions-EMS/endurance-management-system/issues/185
    /// </summary>
    protected Dictionary<string, MudValidationInjector> ValidationInjectors { get; set; } = new();

    [Parameter]
    public T Model { get; set; } = default!;

    public virtual T SubmitCreate()
    {
        throw new NotImplementedException();
    }
    public virtual T SubmitUpdate()
    {
        throw new NotImplementedException();
    }
    public virtual void SetUpdateModel(T entity)
    {
        throw new NotImplementedException();
    }
    public virtual void RegisterValidationInjectors()
    {
        throw new NotImplementedException();
    }

    protected override void OnInitialized()
    {
        RegisterValidationInjectors();
    }

    protected void RegisterInjector<TInput>(string field, Func<MudBaseInput<TInput>> mudInputInstanceGetter)
    {
        ValidationInjectors.Add(field, MudValidationInjector.Create(mudInputInstanceGetter));
    }

    protected void RegisterInjector<TInput>(string field, Func<IMudBaseInputWrapper<TInput>> mudInputWrapper)
    {
        ValidationInjectors.Add(field, MudValidationInjector.Create(mudInputWrapper));
    }
    protected void RegisterInjector<TInput>(string field, Func<MudPicker<TInput>> mudInputInstanceGetter)
    {
        ValidationInjectors.Add(field, MudValidationInjector.Create(mudInputInstanceGetter));
    }

    public async Task AddValidationError(string? field, string message)
    {
        if (field == null)
        {
            NotifyHelper.Warn(message);
            return;
        }

        if (!ValidationInjectors.TryGetValue(field, out var injector))
        {
            throw GuardHelper.Exception(
                $"Key '{field}' not found in {nameof(FormTM<T>)}.{nameof(ValidationInjectors)}. " +
                $"Make sure all field components have a ref pointer in there.");
        }

        injector.Inject(message);
        await InvokeAsync(StateHasChanged);
    }

    internal void TriggerRender()
    {
        StateHasChanged();
    }
}
