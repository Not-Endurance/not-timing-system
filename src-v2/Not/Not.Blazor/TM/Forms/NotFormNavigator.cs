using Not.Domain;
using Not.Blazor.Dialogs;
using Not.Blazor.Forms;
using Not.Blazor.Navigation;
using Not.Blazor.TM.Forms.Components;

namespace Not.Blazor.TM.Forms;

public class NotFormNavigator<T, TFields> : IFormNavigator<T, TFields>
    where T : DomainEntity
    where TFields : NotForm<T>
{
    private readonly IDialogs<T, TFields> _notDialogs;
    private readonly ICrumbsNavigator _navigator;

    public NotFormNavigator(IDialogs<T, TFields> notDialogs, ICrumbsNavigator navigator)
    {
        _notDialogs = notDialogs;
        _navigator = navigator;
    }

    public async Task Create()
    {
        await _notDialogs.RenderCreate();
    }

    public Task Update(string endpoint, T entity)
    {
        _navigator.NavigateTo(endpoint, entity);
        return Task.CompletedTask;
    }
}
