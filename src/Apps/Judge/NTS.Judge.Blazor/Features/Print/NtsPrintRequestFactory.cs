using Not.Blazor.Components.Print;
using Not.Files;
using Not.Injection;
using Not.Print;
using NTS.Application.Contracts.Pdf;
using NTS.Blazor.Components.Print;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Judge.Blazor.Features.Print;

public sealed class NtsPrintRequestFactory : INtsPrintRequestFactory, ITransient
{
    readonly INHtmlComponentRenderer _renderer;

    public NtsPrintRequestFactory(INHtmlComponentRenderer renderer)
    {
        _renderer = renderer;
    }

    public async Task<NPrintDocumentRequest> CreateHandouts(
        IReadOnlyList<HandoutDocument> documents,
        NPrintPanelContext context,
        string fileName
    )
    {
        var html = await _renderer.Render<HandoutsPrintDocument>(
            new Dictionary<string, object?>
            {
                [nameof(HandoutsPrintDocument.Documents)] = documents,
                [nameof(HandoutsPrintDocument.Compact)] = true,
                [nameof(HandoutsPrintDocument.LeftLogo)] = ResolveLogo(PrintLogoPath.Fei),
                [nameof(HandoutsPrintDocument.RightLogo)] = ResolveLogo(PrintLogoPath.Bfks),
            }
        );

        return new NPrintDocumentRequest
        {
            Title = Handouts_string,
            FileName = EnsureExtension(fileName, ".pdf"),
            Html = html,
            Page = new NPrintPageOptions
            {
                PaperFormat = context.PaperFormat,
                Orientation = context.Orientation,
                Scale = context.Scale,
                Margin = "6mm",
            },
        };
    }

    public async Task<NPrintDocumentRequest> CreateRanklist(
        ProtocolDocument document,
        NPrintPanelContext context,
        string fileName,
        string? leftLogo,
        string? rightLogo
    )
    {
        var html = await RenderRanklist(document, context, ResolveLogo(leftLogo), ResolveLogo(rightLogo), fileName);
        return CreateRanklistRequest(fileName, html, context);
    }

    public async Task<NPrintBatchRequest> CreateRanklistsZip(
        IReadOnlyList<Ranking> rankings,
        Func<Ranking, ProtocolDocument> createDocument,
        NPrintPanelContext context,
        string fileName,
        string? leftLogo,
        string? rightLogo
    )
    {
        var entries = PdfFileNameHelper.ResultPdfEntries(
            rankings.Select(x => new PdfNamedResult(x.Id, x.Name))
        );
        var documents = new List<NPrintDocumentRequest>();
        foreach (var (result, entryName) in entries)
        {
            var ranking = rankings.Single(x => x.Id == result.Id);
            var document = createDocument(ranking);
            var html = await RenderRanklist(
                document,
                context,
                ResolveLogo(leftLogo),
                ResolveLogo(rightLogo),
                entryName
            );
            documents.Add(CreateRanklistRequest(entryName, html, context));
        }

        return new NPrintBatchRequest
        {
            FileName = EnsureExtension(fileName, ".zip"),
            Documents = documents,
        };
    }

    async Task<string> RenderRanklist(
        ProtocolDocument document,
        NPrintPanelContext context,
        string? leftLogo,
        string? rightLogo,
        string fileName
    )
    {
        _ = context;
        _ = fileName;
        return await _renderer.Render<RanklistPrintDocument>(
            new Dictionary<string, object?>
            {
                [nameof(RanklistPrintDocument.Document)] = document,
                [nameof(RanklistPrintDocument.Compact)] = true,
                [nameof(RanklistPrintDocument.PhasesAsRows)] = true,
                [nameof(RanklistPrintDocument.LeftLogo)] = leftLogo,
                [nameof(RanklistPrintDocument.RightLogo)] = rightLogo,
            }
        );
    }

    static NPrintDocumentRequest CreateRanklistRequest(
        string fileName,
        string html,
        NPrintPanelContext context
    )
    {
        return new NPrintDocumentRequest
        {
            Title = Ranklist_string,
            FileName = EnsureExtension(fileName, ".pdf"),
            Html = html,
            Page = new NPrintPageOptions
            {
                PaperFormat = NPrintPaperFormat.A4,
                Orientation = NPrintOrientation.Portrait,
                Scale = context.Scale,
                Margin = "10mm",
            },
        };
    }

    static string? ResolveLogo(string? logo)
    {
        if (string.IsNullOrWhiteSpace(logo))
        {
            return null;
        }
        if (logo.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return logo;
        }

        var fullPath = Path.IsPathRooted(logo)
            ? logo
            : Path.Combine(Environment.CurrentDirectory, "wwwroot", logo);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        var file = new NFileContent(
            Path.GetFileName(fullPath),
            NFileContentTypes.FromFileName(fullPath),
            File.ReadAllBytes(fullPath)
        );
        return file.ToDataUrl();
    }

    static string EnsureExtension(string fileName, string extension)
    {
        return fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"{fileName}{extension}";
    }
}
