using Not.Blazor.Components;
using Not.Safe;

namespace NTS.Judge.Blazor.Core.Rankings;

public partial class RankingsPage : PrintableComponent
{
    bool _showProtocol = true;

    [Inject]
    IRankingBehind Behind { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Behind);
    }

    async Task ExportFei()
    {
        await SafeHelper.Run(Behind.ExportFei);
    }

    async Task Archive()
    {
        await SafeHelper.Run(Behind.Archive);
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
