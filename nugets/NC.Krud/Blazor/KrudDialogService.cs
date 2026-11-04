using Microsoft.AspNetCore.Components;
using MudBlazor;
using NC.Blazor.Dialogs;
using NC.Blazor.Helpers;
using NC.Exceptions;
using NC.Krud.Abstractions;
using NC.Krud.Blazor.Components.Abstractions;
using NC.Krud.Blazor.Components.Form;
using NC.Krud.Blazor.Dialogs;
using NC.Krud.Models;

namespace NC.Krud.Blazor;

public class KrudDialogService<TModel, TShell>
    where TModel : IKrudFormModel, new()
    where TShell : KrudShell<TModel>
{
    readonly IDialogService _mudDialogService;
    readonly DialogOptions _options = new() { BackdropClick = false };

    public KrudDialogService(IDialogService mudDialogService)
    {
        _mudDialogService = mudDialogService;
    }

    public async Task<TModel?> ShowCreateForm()
    {
        return await Show<KrudFormDialog<TModel, TShell>>(Create_string, []);
    }

    public async Task ShowUpdateForm(TModel model)
    {
        var parameters = new DialogParameters<KrudFormDialog<TModel, TShell>> { { x => x.Model, model } };
        await Show(Update_string, parameters);
    }

    public async Task<bool> ShowCascadingDeleteConfirmation(KrudDeleteImpact impact)
    {
        var parameters = new DialogParameters<KrudCascadingDeleteDialog> { { x => x.Impact, impact } };
        var dialog = await _mudDialogService.ShowAsync<KrudCascadingDeleteDialog>(
            Cascading_Delete_string,
            parameters,
            _options
        );
        return !(await dialog.IsCanceled());
    }

    public async Task<bool> ShowDeleteConfirmation(string item)
    {
        var parameters = new DialogParameters<NDeleteDialog> { { x => x.Item, item } };
        var dialog = await _mudDialogService.ShowAsync<NDeleteDialog>(Delete_string, parameters, _options);
        return !(await dialog.IsCanceled());
    }

    async Task<TModel?> Show<TDialog>(string title, DialogParameters<TDialog> parameters)
        where TDialog : IComponent
    {
        var dialog = await _mudDialogService.ShowAsync<TDialog>(title, parameters, _options);
        if (await dialog.IsCanceled())
        {
            return default;
        }
        var result = await dialog.Result;
        if (result?.Data is not TModel entity)
        {
            throw GuardHelper.Exception(
                $"Mud dialogr result returned '{result?.Data}'. Expected '{typeof(TModel).FullName}' instead"
            );
        }
        return entity;
    }
}
