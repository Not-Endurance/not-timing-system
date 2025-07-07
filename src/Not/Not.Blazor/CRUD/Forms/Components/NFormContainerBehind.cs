using System.ComponentModel.DataAnnotations;

namespace Not.Blazor.CRUD.Forms.Components;

public class NFormContainerBehind<T, TForm> : NFormContainerBase<T, TForm>
    where TForm : NForm<T>
{
    [Parameter, EditorRequired]
    public Func<Task> FormAction { get; set; } = default!;

    [Parameter, EmailAddress]
    public string ButtonText { get; set; } = default!;

    public override async Task Submit()
    {
        await FormAction();
    }
}
