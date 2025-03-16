using MudBlazor;
using Not.Blazor.CRUD.Forms.Components;
using Not.Localization;
using Not.Reflection;

namespace Not.Blazor.Dialogs;

public class Dialog<T, TForm>
    where T : new()
    where TForm : NForm<T>
{
    readonly IDialogService _mudDialogService;
    readonly ILocalizer _localizer;
    readonly DialogOptions _options = new() { BackdropClick = false };

    public Dialog(IDialogService mudDialogService, ILocalizer localizer)
    {
        _mudDialogService = mudDialogService;
        _localizer = localizer;
    }

    public async Task RenderCreate()
    {
        await Show<FormCreateDialog<T, TForm>>("Create", []);
    }

    public async Task RenderUpdate(T model)
    {
        var parameters = new DialogParameters<FormUpdateDialog<T, TForm>> { { x => x.Model, model } };
        await Show("Update", parameters);
    }

    async Task Show<TDialog>(string type, DialogParameters<TDialog> parameters)
        where TDialog : IComponent
    {
        var typeName = ReflectionHelper.GetName<T>();
        var title = _localizer.Get(type, " ", typeName);
        var dialog = await _mudDialogService.ShowAsync<TDialog>(title, parameters, _options);
        await dialog.Result;
    }
}
