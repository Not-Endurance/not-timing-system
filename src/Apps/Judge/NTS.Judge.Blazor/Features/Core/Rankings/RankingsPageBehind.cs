using MudBlazor;
using Not.Blazor.Components;
using NTS.Domain.Core.Objects;
using NTS.Judge.Blazor.Features.Core.Rankings.CustomRanking;
using NTS.Judge.Features.Core.Rankings;
using NTS.Judge.Features.Core.Rankings.FeiExport;

namespace NTS.Judge.Blazor.Features.Core.Rankings;

public class RankingsPageBehind : PrintableComponent
{
    public bool IsProtocolVisible = true;

    [Inject]
    IFeiExportService FeiExportService { get; set; } = default!;

    [Inject]
    IRankingService RankingService { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    protected Ranklist Ranklist { get; private set; } = default!;

    //public bool HasContent => RankingService.Ranklist != null;
    public bool IsFeiExportConfigured => Ranklist?.IsFeiExportConfigured ?? false;

    protected override async Task OnInitializedAsync()
    {
        await Observe(RankingService);
    }

    protected override void OnBeforeRender()
    {
        Ranklist = new Ranklist(RankingService.Current);
    }

    protected async Task GenerateFeiExport()
    {
        try
        {
            await FeiExportService.Create(Ranklist);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task ArchiveEnduranceEvent()
    {
        try
        {
            await RankingService.ArchiveEnduranceEvent();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task OpenCustomRankingDialog()
    {
        try
        {
            var options = new DialogOptions { FullWidth = true, MaxWidth = MaxWidth.Medium };
            await DialogService.ShowAsync<CreateCustomRankingDialog>("", options);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected void ShowProtocol()
    {
        IsProtocolVisible = true;
    }

    protected void ShowRanklist()
    {
        IsProtocolVisible = false;
    }
}
