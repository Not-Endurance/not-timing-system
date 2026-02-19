using Microsoft.AspNetCore.Components;
using MudBlazor;
using Not.Blazor.Components;
using Not.Krud.Abstractions;
using Not.Krud.Blazor.Components.Abstractions;

namespace Not.Krud.Blazor.Components.Form;

public class KrudFormDialogBehind<TModel, TForm> : NDialog<TModel>
    where TForm : KrudShell<TModel>
    where TModel : IKrudFormModel, new()
{
    protected KrudDynamicForm<TForm, TModel>? DynamicFormRef { get; set; } = default!;

    [CascadingParameter]
    protected MudDialogInstance Dialog { get; set; } = default!;

    [Parameter]
    public TModel Model { get; set; } = new();
}
