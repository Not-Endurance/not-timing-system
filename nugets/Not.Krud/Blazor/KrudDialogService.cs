using Microsoft.AspNetCore.Components;
using MudBlazor;
using Not.Exceptions;
using Not.Krud.Blazor.Components;

namespace Not.Krud.Blazor;

public class KrudDialogService<T, TForm>
    where T : new()
    where TForm : KrudFormBehindNotSure<T>
{
    readonly IDialogService _mudDialogService;
    readonly DialogOptions _options = new() { BackdropClick = false };

    public KrudDialogService(IDialogService mudDialogService)
    {
        _mudDialogService = mudDialogService;
    }

    public async Task<T> ShowCreateForm()
    {
        return await Show<KrudCreateFormDialog<T, TForm>>(Create_string, []);
    }

    public async Task ShowUpdateForm(T model)
    {
        var parameters = new DialogParameters<KrudUpdateFormDialog<T, TForm>> { { x => x.Model, model } };
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
