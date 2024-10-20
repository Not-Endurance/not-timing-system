﻿using Not.Domain;
using MudBlazor;
using Not.Blazor.Components;
using Not.Blazor.Forms;
using Not.Exceptions;
using Not.Notifier;

namespace Not.Blazor.TM.Forms.Components;

// TODO: https://github.com/Not-Endurance/not-timing-system/issues/267
public abstract class NotForm<T> : NotComponent, ICreateForm<T>, IUpdateForm<T>
    where T : DomainEntity
{
    /// <summary>
    /// Contains refs to the actual field components, necessary in order to render Mud validation messages from the DomainException
    /// This is a workaround until: https://github.com/eSolutions-EMS/endurance-management-system/issues/185
    /// </summary>
    protected Dictionary<string, MudValidationInjector> ValidationInjectors { get; set; } = new();

    public abstract T SubmitCreate();
    public abstract T SubmitUpdate();
    public abstract void SetUpdateModel(T entity);
    public abstract void RegisterValidationInjectors();

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
                $"Key '{field}' not found in {nameof(NotForm<T>)}.{nameof(ValidationInjectors)}. " +
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
