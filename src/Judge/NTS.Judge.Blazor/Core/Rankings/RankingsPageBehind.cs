using MudBlazor;
using Not.Blazor.Components;
using Not.Safe;

namespace NTS.Judge.Blazor.Core.Rankings;

public class RankingsPageBehind : PrintableComponent
{
    public bool IsProtocolVisible = true;

    [Inject]
    IRankingService Service { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    public bool HasContent => Service.Ranklist != null;
    
    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }

    public async Task GenerateFeiExport()
    {
        await SafeHelper.Run(Service.GenerateFeiExport);
    }

    public async Task ArchiveEnduranceEvent()
    {
        await SafeHelper.Run(Service.ArchiveEnduranceEvent);
    }
    
    public void ShowProtocol()
    {
        IsProtocolVisible = true;
    }

    public void ShowRanklist()
    {
        IsProtocolVisible = false;
    }
}
