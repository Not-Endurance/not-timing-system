using System.IO.Compression;
using Not.Files;
using Not.Print;

namespace Not.Server.Print;

public sealed class PlaywrightPrintService : INPrintService
{
    readonly INPrintRenderer _renderer;
    readonly INPrintTemplateRenderer _templates;

    public PlaywrightPrintService(INPrintTemplateRenderer templates, INPrintRenderer renderer)
    {
        _templates = templates;
        _renderer = renderer;
    }

    public async Task<NFileContent> CreatePdf(
        NPrintDocumentRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var html = _templates.Render(request);
        var content = await _renderer.Render(html, cancellationToken);
        return new NFileContent(request.FileName, NFileContentTypes.Pdf, content);
    }

    public async Task<NFileContent> CreateZip(
        NPrintBatchRequest request,
        CancellationToken cancellationToken = default
    )
    {
        await using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var document in request.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pdf = await CreatePdf(document, cancellationToken);
                var entry = archive.CreateEntry(pdf.FileName, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(pdf.Content, cancellationToken);
            }
        }

        return new NFileContent(request.FileName, NFileContentTypes.Zip, stream.ToArray());
    }

    public Task PrintPdf(
        NPrintDocumentRequest request,
        CancellationToken cancellationToken = default
    )
    {
        throw BrowserOnly();
    }

    public Task DownloadPdf(
        NPrintDocumentRequest request,
        CancellationToken cancellationToken = default
    )
    {
        throw BrowserOnly();
    }

    public Task DownloadZip(
        NPrintBatchRequest request,
        CancellationToken cancellationToken = default
    )
    {
        throw BrowserOnly();
    }

    public Task DownloadFile(NFileContent file, CancellationToken cancellationToken = default)
    {
        throw BrowserOnly();
    }

    static NotSupportedException BrowserOnly()
    {
        return new("Browser print actions are not available on the server.");
    }
}
