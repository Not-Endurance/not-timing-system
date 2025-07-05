using Not.Blazor.Components;
using Not.Safe;

namespace NTS.Judge.Blazor.Core.Rankings;

public partial class RankingsPage : PrintableComponent
{
    bool _showProtocol;

    [Inject]
    IRankingBehind _behind { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(_behind);
    }

    async Task ExportFei()
    {
        await SafeHelper.Run(_behind.ExportFei);
    }

    async Task Archive()
    {
        await SafeHelper.Run(_behind.Archive);
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
