using MudBlazor;
using Not.Blazor.Components;
using Not.Safe;
using NTS.Judge.Blazor.Core.Rankings.CustomRanking;
using NTS.Judge.Features.Core.Rankings;
using NTS.Judge.Features.Core.Rankings.FeiExport;

namespace NTS.Judge.Blazor.Core.Rankings;

public class RankingsPageBehind : PrintableComponent
{
    public bool IsProtocolVisible = true;

    [Inject]
    IFeiExportService FeiExportService { get; set; } = default!;

    [Inject]
    IRankingService RankingService { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    public bool HasContent => RankingService.Ranklist != null;
    public bool IsFeiExportConfigured => RankingService.Ranklist?.IsFeiExportConfigured ?? false;

    protected override async Task OnInitializedAsync()
    {
        await Observe(RankingService);
    }

    public async Task GenerateFeiExport()
    {
        await SafeHelper.Run(() => FeiExportService.Create(RankingService.Ranklist!));
    }

    public async Task ArchiveEnduranceEvent()
    {
        await SafeHelper.Run(RankingService.ArchiveEnduranceEvent);
    }

    public async Task OpenCustomRankingDialog()
    {
        await DialogService.ShowAsync<CreateCustomRankingDialog>(
            "",
            new DialogOptions { FullWidth = true, MaxWidth = MaxWidth.Medium }
        );
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
