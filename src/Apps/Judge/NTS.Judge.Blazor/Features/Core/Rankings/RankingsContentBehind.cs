using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Dialogs;
using Not.Blazor.Helpers;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;
using NTS.Judge.Blazor.Features.Core.Rankings.CustomRanking;
using NTS.Judge.Blazor.Features.Core.Rankings.Protocols;
using NTS.Judge.Contracts.Features.Core.Rankings;
using NTS.Judge.Contracts.Features.Core.Rankings.FeiExport;

namespace NTS.Judge.Blazor.Features.Core.Rankings;

public class RankingsContentBehind : PrintableComponent
{
    TaskCompletionSource<bool>? _renderCompletionSource;

    [Inject]
    IFeiExportService FeiExportService { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    IProtocolDocumentService DocumentService { get; set; } = default!;

    [Inject]
    protected IRankingMenuService RankingService { get; set; } = default!;

    protected bool CompactParticipationTables { get; private set; }

    protected Ranklist Ranklist { get; private set; } = default!;

    protected ProtocolDocument? Document { get; private set; }

    [Inject]
    protected IProtocolLogoState HeaderLogo { get; set; } = default!;

    //public bool HasContent => RankingService.Ranklist != null;
    public bool IsFeiExportConfigured => Ranklist?.IsFeiExportConfigured ?? false;

    protected override async Task OnInitializedAsync()
    {
        await Observe(RankingService);
    }

    protected override void OnBeforeRender()
    {
        Ranklist = new Ranklist(RankingService.Current);
        Document = DocumentService.Create(RankingService.Current);
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

    protected void SelectRanking(Ranking ranking)
    {
        try
        {
            RankingService.Select(ranking);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task OpenDeleteDialog(Ranking ranking)
    {
        try
        {
            var arguments = new DialogParameters<NDeleteDialog> { { x => x.Item, ranking.Name } };
            var dialog = await DialogService.ShowAsync<NDeleteDialog>(Delete_string, arguments);
            if (await dialog.IsCanceled())
            {
                return;
            }

            await RankingService.Delete(ranking);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task OpenImageBrowser(string forImage)
    {
        try
        {
            var parameters = new DialogParameters<ImageBrowserDialog>
            {
                { x => x.SelectedImagePath, forImage },
                { x => x.Src, HeaderLogo.DirPath },
            };
            DialogOptions options = new()
            {
                MaxWidth = MaxWidth.ExtraLarge,
                FullWidth = true,
                CloseOnEscapeKey = true,
            };
            var dialog = await DialogService.ShowAsync<ImageBrowserDialog>(
                Image_browser_string,
                parameters,
                options
            );
            await dialog.Result;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
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
