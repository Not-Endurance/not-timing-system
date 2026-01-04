using MudBlazor;

namespace Not.Blazor.CRUD.Forms.Validation;

public interface IMudBaseInputWrapper<T>
{
    MudBaseInput<T> MudBaseInput { get; }
}
