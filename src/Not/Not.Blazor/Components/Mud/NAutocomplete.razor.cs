using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Not.Blazor.Ports;

namespace Not.Blazor.Components;

public partial class NAutocomplete<T> : NBindableComponent<T>
{
    [Inject]
    IServiceProvider Provider { get; set; } = default!;

    [Parameter]
    public Func<string, Task<IEnumerable<T>>>? Search { get; set; } = default!;

    [Parameter]
    public bool ResetValueOnClick { get; set; } = true;

    [Parameter]
    public string Label { get; set; } = "";

    public MudBaseInput<T> MudBaseInput { get; private set; } = default!;

    protected override void OnInitialized()
    {
        var seeker = Provider.GetService<ISeeker<T>>();
        if (Search == null && seeker == null)
        {
            throw GuardHelper.Exception(
                $"NAutocomplete cannot work without search provider. Either define '{nameof(ISeeker<T>)}' implementation or provide '{nameof(Search)}' parameter"
            );
        }
        Search ??= seeker!.Search;
    }

    Task<IEnumerable<T>> Seek(string term, CancellationToken _)
    {
        return Search!.Invoke(term ?? "");
    }

    void HandleOnClick(MouseEventArgs _)
    {
        if (ResetValueOnClick)
        {
            Value = default;
        }
    }
}
