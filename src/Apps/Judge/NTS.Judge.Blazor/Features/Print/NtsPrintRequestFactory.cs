using System.Text;
using Not.Blazor.Components.Print;
using Not.Injection;
using Not.Print;
using NTS.Application.Contracts;
using NTS.Application.Contracts.Pdf;
using NTS.Blazor.Components.Results;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Judge.Blazor.Features.Print;

public sealed class NtsPrintRequestFactory : INtsPrintRequestFactory, ITransient
{
    const string HANDOUT_PAGE_MARGIN = "6mm 6mm 10mm 6mm";
    const string RANKLIST_PAGE_MARGIN = "10mm 10mm 12mm 10mm";

    readonly INHtmlComponentRenderer _renderer;

    public NtsPrintRequestFactory(INHtmlComponentRenderer renderer)
    {
        _renderer = renderer;
    }

    static string GeneratedByText => $"{Generated_by_NoTiming_System_v_string}{ApplicationConstants.VERSION}";

    public async Task<NPrintDocumentRequest> CreateHandouts(
        IReadOnlyList<ResultsDocument> documents,
        NPrintPanelContext context,
        string fileName
    )
    {
        var html = await RenderResults(documents, LogoConstants.Fei, LogoConstants.Bfks);

        return new NPrintDocumentRequest
        {
            Title = Handouts_string,
            FileName = EnsureExtension(fileName, ".pdf"),
            Html = html,
            FooterText = GeneratedByText,
            BackdropImage = LogoConstants.Nts,
            Page = new NPrintPageOptions
            {
                PaperFormat = context.PaperFormat,
                Orientation = context.Orientation,
                Scale = context.Scale,
                Margin = HANDOUT_PAGE_MARGIN,
            },
        };
    }

    public async Task<NPrintDocumentRequest> CreateRanklist(
        ResultsDocument document,
        NPrintPanelContext context,
        string fileName,
        string? leftLogo,
        string? rightLogo
    )
    {
        var html = await RenderResult(document, leftLogo, rightLogo);
        return CreateRanklistRequest(fileName, html, context);
    }

    public async Task<NPrintBatchRequest> CreateRanklistsZip(
        IReadOnlyList<Ranking> rankings,
        Func<Ranking, ResultsDocument> createDocument,
        NPrintPanelContext context,
        string fileName,
        string? leftLogo,
        string? rightLogo
    )
    {
        var entries = PdfFileNameHelper.ResultPdfEntries(rankings.Select(x => new PdfNamedResult(x.Id, x.Name)));
        var documents = new List<NPrintDocumentRequest>();
        foreach (var (result, entryName) in entries)
        {
            var ranking = rankings.Single(x => x.Id == result.Id);
            var document = createDocument(ranking);
            var html = await RenderResult(document, leftLogo, rightLogo);
            documents.Add(CreateRanklistRequest(entryName, html, context));
        }

        return new NPrintBatchRequest { FileName = EnsureExtension(fileName, ".zip"), Documents = documents };
    }

    async Task<string> RenderResult(ResultsDocument document, string? leftLogo, string? rightLogo)
    {
        return await _renderer.Render<ResultComponent>(
            new Dictionary<string, object?>
            {
                [nameof(ResultComponent.Document)] = document,
                [nameof(ResultComponent.LeftLogo)] = leftLogo,
                [nameof(ResultComponent.RightLogo)] = rightLogo,
            }
        );
    }

    async Task<string> RenderResults(IReadOnlyList<ResultsDocument> documents, string? leftLogo, string? rightLogo)
    {
        var html = new StringBuilder();
        foreach (var document in documents)
        {
            if (html.Length > 0)
            {
                html.AppendLine();
            }

            html.Append(await RenderResult(document, leftLogo, rightLogo));
        }

        return html.ToString();
    }

    static NPrintDocumentRequest CreateRanklistRequest(string fileName, string html, NPrintPanelContext context)
    {
        return new NPrintDocumentRequest
        {
            Title = Ranklist_string,
            FileName = EnsureExtension(fileName, ".pdf"),
            Html = html,
            FooterText = GeneratedByText,
            BackdropImage = LogoConstants.Nts,
            Page = new NPrintPageOptions
            {
                PaperFormat = NPrintPaperFormat.A4,
                Orientation = NPrintOrientation.Portrait,
                Scale = context.Scale,
                Margin = RANKLIST_PAGE_MARGIN,
            },
        };
    }

    static string EnsureExtension(string fileName, string extension)
    {
        return fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? fileName : $"{fileName}{extension}";
    }
}

public interface INtsPrintRequestFactory
{
    Task<NPrintDocumentRequest> CreateHandouts(
        IReadOnlyList<ResultsDocument> documents,
        NPrintPanelContext context,
        string fileName
    );

    Task<NPrintDocumentRequest> CreateRanklist(
        ResultsDocument document,
        NPrintPanelContext context,
        string fileName,
        string? leftLogo,
        string? rightLogo
    );

    Task<NPrintBatchRequest> CreateRanklistsZip(
        IReadOnlyList<Ranking> rankings,
        Func<Ranking, ResultsDocument> createDocument,
        NPrintPanelContext context,
        string fileName,
        string? leftLogo,
        string? rightLogo
    );
}
