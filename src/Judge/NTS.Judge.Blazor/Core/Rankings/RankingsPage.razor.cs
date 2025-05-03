using Not.Blazor.Components;

namespace NTS.Judge.Blazor.Core.Rankings;

public partial class RankingsPage : PrintableComponent
{
    bool _showProtocol;

    [Inject]
    IRankingBehind Behind { get; set; } = default!;

    [Inject]
    IRankingDocumentBehind DocumentBehind { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Behind);
    }

    void ShowProtocol()
    {
        _showProtocol = true;
    }

    void ShowRanklist()
    {
        _showProtocol = false;
    }
}
