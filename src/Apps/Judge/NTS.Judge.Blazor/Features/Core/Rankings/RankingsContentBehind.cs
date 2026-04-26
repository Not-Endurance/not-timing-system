using MudBlazor;
using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Objects;
using NTS.Judge.Blazor.Features.Core.Rankings.CustomRanking;
using NTS.Judge.Contracts.Features.Core.Rankings;
using NTS.Judge.Contracts.Features.Core.Rankings.FeiExport;

namespace NTS.Judge.Blazor.Features.Core.Rankings;

public class RankingsContentBehind : PrintableComponent
{
    TaskCompletionSource<bool>? _renderCompletionSource;
    public bool IsProtocolVisible = true;

    [Inject]
    IFeiExportService FeiExportService { get; set; } = default!;

    [Inject]
    IRankingService RankingService { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    protected bool CompactParticipationTables { get; private set; }

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

    protected async Task OpenCustomRankingDialog()
    {
        try
        {
            var options = new DialogOptions { FullWidth = true, MaxWidth = MaxWidth.Medium };
            await DialogService.ShowAsync<CustomRankingDialog>("", options);
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

    protected async Task Print()
    {
        try
        {
            CompactParticipationTables = true;
            await WaitForRender();
            await OpenPrintDialog();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
        finally
        {
            CompactParticipationTables = false;
            await WaitForRender();
        }
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        _renderCompletionSource?.TrySetResult(true);
        _renderCompletionSource = null;
        return Task.CompletedTask;
    }

    async Task WaitForRender()
    {
        _renderCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await InvokeRender();
        await _renderCompletionSource.Task;
    }
}
