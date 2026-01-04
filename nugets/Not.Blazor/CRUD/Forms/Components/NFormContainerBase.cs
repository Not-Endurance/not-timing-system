using Not.Blazor.Components;
using Not.Blazor.Navigation;

namespace Not.Blazor.CRUD.Forms.Components;

public abstract class NFormContainerBase<T, TForm> : NBehind
    where TForm : NForm<T>
{
    public NDynamic<T, TForm> Form = default!;

    public abstract Task Submit();

    [Inject]
    ICrumbsNavigator Navigator { get; set; } = default!;

    [Parameter]
    public T Model { get; set; } = default!;

    [Parameter]
    public bool DisableBack { get; set; }

    protected async Task InjectValidation(ValidationException validation)
    {
        await Form!.Instance.AddValidationError(validation.Property, validation.Message);
    }

    public bool CanNavigateBack()
    {
        return !DisableBack && Navigator.CanNavigateBack();
    }

    public void NavigateBack()
    {
        if (CanNavigateBack())
        {
            Navigator.NavigateBack();
        }
    }
}
