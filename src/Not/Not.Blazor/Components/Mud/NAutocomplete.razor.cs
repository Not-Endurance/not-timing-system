using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Not.Blazor.Ports;

namespace Not.Blazor.Components;

public partial class NAutocomplete<T>
{
    [Parameter]
    public Func<string, Task<IEnumerable<T>>>? Search { get; set; } = default!;

    [Parameter]
    public bool ResetValueOnClick { get; set; } = true;

    [Parameter]
    public string Label { get; set; } = "";

    [Inject]
    public ISeeker<T>? Seeker { get; set; }

    public MudBaseInput<T> MudBaseInput { get; private set; } = default!;

    protected override void OnInitialized()
    {
        if (Search == null && Seeker == null)
        {
            throw GuardHelper.Exception($"NAutocomplete cannot work without search provider. Either define '{nameof(ISeeker<T>)}' implementation or provide '{nameof(Search)}' parameter");
        }
        Search ??= Seeker!.Search;
    }

    Task<IEnumerable<T>> Seek(string term, CancellationToken _)
    {
        return Search!.Invoke(term);
    }

    void HandleOnClick(MouseEventArgs _)
    {
        if (ResetValueOnClick)
        {
            Value = default;
        }
    }
}
