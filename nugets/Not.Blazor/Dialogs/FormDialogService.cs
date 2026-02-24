using MudBlazor;
using Not.Blazor.CRUD.Forms.Components;
using Not.Blazor.Dialogs.Components;

namespace Not.Blazor.Dialogs;

public class FormDialogService<T, TForm>
    where T : new()
    where TForm : NForm<T>
{
    readonly IDialogService _mudDialogService;
    readonly DialogOptions _options = new() { BackdropClick = false };

    public FormDialogService(IDialogService mudDialogService)
    {
        _mudDialogService = mudDialogService;
    }

    public async Task<T> ShowCreateForm()
    {
        return await Show<FormCreateDialog<T, TForm>>(Create_string, []);
    }

    public async Task ShowUpdateForm(T model)
    {
        var parameters = new DialogParameters<FormUpdateDialog<T, TForm>> { { x => x.Model, model } };
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
