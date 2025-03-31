using MudBlazor;
using Not.Blazor.CRUD.Forms.Components;

namespace Not.Blazor.Dialogs;

public class Dialog<T, TForm>
    where T : new()
    where TForm : NForm<T>
{
    readonly IDialogService _mudDialogService;
    readonly DialogOptions _options = new() { BackdropClick = false };

    public Dialog(IDialogService mudDialogService)
    {
        _mudDialogService = mudDialogService;
    }

    public async Task RenderCreate()
    {
        await Show<FormCreateDialog<T, TForm>>(Create_string, []);
    }

    public async Task RenderUpdate(T model)
    {
        var parameters = new DialogParameters<FormUpdateDialog<T, TForm>> { { x => x.Model, model } };
        await Show(Update_string, parameters);
    }

    async Task Show<TDialog>(string title, DialogParameters<TDialog> parameters)
        where TDialog : IComponent
    {
        var dialog = await _mudDialogService.ShowAsync<TDialog>(title, parameters, _options);
        await dialog.Result;
    }
}
