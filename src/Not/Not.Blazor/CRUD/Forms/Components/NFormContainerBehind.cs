using System.ComponentModel.DataAnnotations;

namespace Not.Blazor.CRUD.Forms.Components;

public class NFormContainerBehind<T, TForm> : NFormContainerBase<T, TForm>
    where TForm : NForm<T>
{
    [Parameter, Required]
    public Func<Task> FormAction { get; set; } = default!;

    public override async Task Submit()
    {
        await FormAction();
    }
}
