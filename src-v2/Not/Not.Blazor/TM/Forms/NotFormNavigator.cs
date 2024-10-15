using Not.Blazor.Navigation;
using Not.Blazor.TM.Forms.Components;
using Not.Blazor.TM.Dialogs;

namespace Not.Blazor.TM.Forms;

public class FormManager<T, TForm>
    where T : new()
    where TForm : FormTM<T>
{
    private readonly DialogTM<T, TForm> _dialog;
    private readonly ICrumbsNavigator _navigator;

    public FormManager(DialogTM<T, TForm> dialog, ICrumbsNavigator navigator)
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
