using Not.Blazor.CRUD.Forms.Components;
using Not.Blazor.Dialogs;
using Not.Blazor.Navigation;

namespace Not.Blazor.CRUD.Forms;

public class FormManager<T, TForm>
    where T : new()
    where TForm : NForm<T>
{
    readonly CrudeDialog<T, TForm> _dialog;
    readonly ICrumbsNavigator _navigator;

    public FormManager(CrudeDialog<T, TForm> dialog, ICrumbsNavigator navigator)
    {
        _dialog = dialog;
        _navigator = navigator;
    }

    public async Task Create()
    {
        await _dialog.RenderCreate();
    }

    public Task Update(string endpoint, T entity)
    {
        _navigator.NavigateTo(endpoint, entity);
        return Task.CompletedTask;
    }
}
