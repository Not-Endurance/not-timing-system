using Not.Application.Services;

namespace Not.Blazor.CRUD.Forms.Components;

public class NUpdateContainerBehind<T, TForm> : NFormContainerBase<T, TForm>
    where TForm : NForm<T>
{
    [Inject]
    IUpdateBehind<T> Behind { get; set; } = default!;

    public override async Task Submit()
    {
        await Behind.Update(Model);
        NavigateBack();
    }
}
