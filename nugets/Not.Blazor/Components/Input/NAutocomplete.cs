using MudBlazor;
using Not.Blazor.Components.Input.Internal;
using Not.Safe;

namespace Not.Blazor.Components.Input;

public class NAutocomplete<T> : MudAutocomplete<T?>
{
    public NAutocomplete()
    {
        CoerceText = true;
        ResetValueOnEmptyText = true;
        ShowProgressIndicator = true;
        SelectOnActivation = false;
        SearchFunc = (term, ct) => SafeHelper.RunWithError(() => SearchSafe(term, ct));
    }

	/// <summary>
	/// Calls <see cref="MudAutocomplete{T}.SearchFunc" /> within a try-catch block
	/// </summary>
	[Parameter]
    public Func<string, CancellationToken, Task<IEnumerable<T?>>> SearchSafe { get; set; } = default!;

    protected override void OnParametersSet()
    {
        ForRequiredValidator.ValidateFor(this);
        base.OnParametersSet();
    }
}
