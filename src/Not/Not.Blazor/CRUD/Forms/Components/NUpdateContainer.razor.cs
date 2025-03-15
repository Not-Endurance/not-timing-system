using Not.Blazor.Components;
using Not.Blazor.CRUD.Ports;
using Not.Blazor.Navigation;
using Not.Safe;

namespace Not.Blazor.CRUD.Forms.Components;

public partial class NUpdateContainer<T, TForm>
    where TForm : NForm<T>
{
    NDynamic<T, TForm> _form = default!;

    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    [Inject]
    IUpdateBehind<T> Behind { get; set; } = default!;

    [Parameter]
    public T Model { get; set; } = default!;

    async Task Update()
    {
        await SafeHelper.Run(Submit, InjectValidation);
    }

    async Task Submit()
    {
        await Behind.Update(Model);
        NavigateBack();
    }

    async Task InjectValidation(ValidationException validation)
    {
        await _form!.Instance.AddValidationError(validation.Property, validation.Message);
    }

    void NavigateBack()
    {
        Navigator.NavigateBack();
    }
}
