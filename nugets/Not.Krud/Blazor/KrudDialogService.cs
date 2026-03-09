using Microsoft.AspNetCore.Components;
using MudBlazor;
using Not.Blazor.Helpers;
using Not.Exceptions;
using Not.Krud.Abstractions;
using Not.Krud.Blazor.Components.Abstractions;
using Not.Krud.Blazor.Components.Form;
using Not.Krud.Blazor.Dialogs;
using Not.Krud.Models;

namespace Not.Krud.Blazor;

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
        var dialog = await _mudDialogService.ShowAsync<KrudCascadingDeleteDialog>(Cascading_Delete_string, parameters, _options);
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
