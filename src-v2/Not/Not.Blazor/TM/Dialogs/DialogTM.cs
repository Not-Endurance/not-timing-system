using Not.Services;
using MudBlazor;
using Not.Blazor.TM.Forms.Components;
using Not.Reflection;

namespace Not.Blazor.TM.Dialogs;

public class DialogTM<T, TForm>
    where T : new()
    where TForm : FormTM<T>
{
    private readonly IDialogService _mudDialogService;
    private readonly ILocalizer _localizer;
    private readonly DialogOptions _options = new DialogOptions { BackdropClick = false };

    public DialogTM(IDialogService mudDialogService, ILocalizer localizer)
    {
        _mudDialogService = mudDialogService;
        _localizer = localizer;
    }

    public async Task RenderCreate()
    {
        await Show<CreateFormDialog<T, TForm>>("Create", []);
    }

    public async Task RenderUpdate(T model)
    {
        var parameters = new DialogParameters<UpdateFormDialog<T, TForm>>
        {
            { x => x.Model, model }
        };
        await Show("Update", parameters);
    }

    async Task Show<TDialog>(string type, DialogParameters<TDialog> parameters)
        where TDialog : IComponent
    {
        var title = _localizer.Get(type, " ", ReflectionHelper.GetName<T>());
        var dialog = await _mudDialogService.ShowAsync<TDialog>(title, parameters, _options);
        await dialog.Result;
    }
}