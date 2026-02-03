using Microsoft.AspNetCore.Components;
using MudBlazor;
using Not.Application.Services;
using Not.Exceptions;
using Not.Krud.Blazor.Components;
using Not.Krud.Blazor.Components.Abstractions;

namespace Not.Krud.Blazor;

public class KrudDialogService<T, TForm>
    where T : new()
    where TForm : KrudFormContainer<T>
{
    readonly IDialogService _mudDialogService;
    readonly IFormBehind<T> _service;
    readonly DialogOptions _options = new() { BackdropClick = false };

    public KrudDialogService(IDialogService mudDialogService, IFormBehind<T> service)
    {
        _mudDialogService = mudDialogService;
        _service = service;
    }

    public async Task<T> ShowCreateForm()
    {
        var parameters = new DialogParameters<KrudFormDialog<T, TForm>> { { x => x.Submit, _service.Create } };
        return await Show(Create_string, parameters);
    }

    public async Task ShowUpdateForm(T model)
    {
        var parameters = new DialogParameters<KrudFormDialog<T, TForm>> { 
            { x => x.Model, model },
            { x => x.Submit, _service.Update }
        };
        await Show(Update_string, parameters);
    }

    async Task<T> Show<TDialog>(string title, DialogParameters<TDialog> parameters)
        where TDialog : IComponent
    {
        var dialog = await _mudDialogService.ShowAsync<TDialog>(title, parameters, _options);
        var result = await dialog.Result;
        if (result?.Data is not T entity)
        {
            throw GuardHelper.Exception($"Mud dialogr result returned '{result?.Data}'. Expected '{typeof(T).FullName}' instead");
        }
        return entity;
    }
}
