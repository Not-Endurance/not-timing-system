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

    public async Task<NFile> CreatePdf(
        NPrintDocumentRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var html = _templates.Render(request);
        var content = await _renderer.Render(html, cancellationToken);
        return new NFile(request.FileName, NFileContentTypes.Pdf, content);
    }

    public async Task<NFile> CreateZip(
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
                var entry = archive.CreateEntry(pdf.Name, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(pdf.Content, cancellationToken);
            }
        }

        return new NFile(request.FileName, NFileContentTypes.Zip, stream.ToArray());
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

    public Task DownloadFile(NFile file, CancellationToken cancellationToken = default)
    {
        throw BrowserOnly();
    }

    static NotSupportedException BrowserOnly()
    {
        return new("Browser print actions are not available on the server.");
    }
}
