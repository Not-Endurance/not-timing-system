using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Components.Print;
using Not.Blazor.Dialogs;
using Not.Blazor.Helpers;
using Not.Domain.Exceptions;
using Not.Files;
using Not.Files.Abstractions;
using Not.Notify;
using Not.Print;
using NoTiming.Console.Features.Printing;
using NoTiming.Console.Features.Rankings.CustomRanking;
using NoTiming.Console.Features.Rankings.Fei;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Console;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents;

namespace NoTiming.Console.Features.Rankings;

/// <summary>
/// The Rankings of the Event the page opened, and the Results of the one the person picked (#646): printed through the
/// browser with the logos of the Tenant, exported for the FEI as a file the browser downloads, made new from the Participations
/// the Main Operator chooses while the Event is Live, and removed. The Results are composed from the Participations whenever
/// they are shown (ADR-0006), so the page shows a Live Event as it is now and a Historic Event as a record.
/// </summary>
public class RankingsPageBehind : NStatefulComponent
{
    const decimal DEFAULT_PRINT_SCALE = 0.85m;

    Ranking? _selected;

    [Inject]
    IConsoleRankings Rankings { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    INHtmlComponentRenderer Renderer { get; set; } = default!;

    [Inject]
    IFileService Files { get; set; } = default!;

    [Inject]
    INotifier Notifier { get; set; } = default!;

    protected ResultsDocument? Document { get; private set; }
    protected IReadOnlyList<Ranking> Ranked => View.Rankings;
    protected Ranking? CurrentRanking => _selected;
    protected bool CanChange => View.CanWrite;
    protected bool CanExport => View.Rankings.Any(FeiExport.IsConfigured);
    protected bool CanPrint => Document != null;
    protected decimal PrintFontScale { get; set; } = DEFAULT_PRINT_SCALE;
    protected EventCallback<Ranking> OnDelete =>
        CanChange ? EventCallback.Factory.Create<Ranking>(this, OpenDeleteDialog) : default;
    protected string? LeftLogo => null;
    protected string? RightLogo => null;

    protected IReadOnlyList<NPrintPanelAction> PrintActions =>
        [NPrintPanelAction.PrintPdf(Print_string, CreatePrintRequest, Icons.Material.Outlined.Print)];

    [CascadingParameter]
    public IViewedEvent View { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        Rankings.Show(View);
        _selected = View.Rankings.FirstOrDefault();
        await Observe(View);
    }

    protected override void OnBeforeRender()
    {
        _selected = View.Rankings.FirstOrDefault(x => x.Id == _selected?.Id) ?? View.Rankings.FirstOrDefault();
        Document = _selected == null ? null : View.CreateDocument(_selected);
    }

    protected void SelectRanking(Ranking ranking)
    {
        _selected = Ranked.FirstOrDefault(x => x.Id == ranking.Id) ?? ranking;
        Document = View.CreateDocument(_selected);
    }

    protected async Task OpenCustomRankingDialog()
    {
        try
        {
            var options = new DialogOptions { FullWidth = true, MaxWidth = MaxWidth.Medium };
            var parameters = new DialogParameters<CustomRankingDialog> { { x => x.View, View } };
            await DialogService.ShowAsync<CustomRankingDialog>(string.Empty, parameters, options);
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

            await Rankings.Delete(ranking);
        }
        catch (DomainException ex)
        {
            Notifier.Warn(ex.Message);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task ExportForTheFei()
    {
        try
        {
            var rankings = View.Rankings.ToList();
            var results = rankings.Select(x => View.CreateDocument(x).Results).ToList();
            var document = new FeiExport().Create(View.Event, results, rankings);
            await Files.Download(NFile.FromText(document.FileName, document.Content, document.ContentType));
        }
        catch (DomainException ex)
        {
            Notifier.Warn(ex.Message);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    Task<NPrintDocumentRequest> CreatePrintRequest(NPrintPanelContext context)
    {
        var document = Document ?? throw new DomainException(Empty_string);
        return new PrintRequests(Renderer).Ranklist(document, context, LeftLogo, RightLogo);
    }
}
