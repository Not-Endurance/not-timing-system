using System.IO.Compression;
using Not.Injection;
using Not.Storage.Mongo;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.Pdf;

namespace NTS.Nexus.HTTP.Functions.Pdf;

public interface IPdfGenerationService : ITransient
{
    Task<PdfGeneratedFile> Create(PdfDocumentRequest request, CancellationToken cancellationToken);
    Task<PdfGeneratedFile> CreateResultsZip(PdfResultsZipRequest request, CancellationToken cancellationToken);
}

public class PdfGenerationService : IPdfGenerationService
{
    readonly IPdfPrintUrlFactory _printUrlFactory;
    readonly IPdfRenderer _renderer;
    readonly IMongoRepository<RankingModel> _rankings;

    public PdfGenerationService(
        IPdfPrintUrlFactory printUrlFactory,
        IPdfRenderer renderer,
        IMongoRepository<RankingModel> rankings
    )
    {
        _printUrlFactory = printUrlFactory;
        _renderer = renderer;
        _rankings = rankings;
    }

    public async Task<PdfGeneratedFile> Create(PdfDocumentRequest request, CancellationToken cancellationToken)
    {
        var url = _printUrlFactory.Create(request);
        var content = await _renderer.Render(url, cancellationToken);
        var fileName = await ResolveFileName(request);
        return new PdfGeneratedFile(fileName, "application/pdf", content);
    }

    public async Task<PdfGeneratedFile> CreateResultsZip(
        PdfResultsZipRequest request,
        CancellationToken cancellationToken
    )
    {
        var rankings = (await _rankings.ReadMany(x => x.EventId == request.EventId)).ToList();
        var entries = PdfFileNameHelper.ResultPdfEntries(rankings.Select(x => new PdfNamedResult(x.Id, x.Name)));

        await using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (result, entryName) in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pdfRequest = new PdfDocumentRequest
                {
                    Type = PdfDocumentType.Ranklist,
                    EventId = request.EventId,
                    RankingId = result.Id,
                    FontScale = request.FontScale,
                };
                var pdf = await _renderer.Render(_printUrlFactory.Create(pdfRequest), cancellationToken);
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(pdf, cancellationToken);
            }
        }

        return new PdfGeneratedFile(
            PdfFileNameHelper.ResultsZip(request.EventId),
            "application/zip",
            stream.ToArray()
        );
    }

    async Task<string> ResolveFileName(PdfDocumentRequest request)
    {
        if (request.Type == PdfDocumentType.Handouts)
        {
            return PdfFileNameHelper.HandoutsPdf(request.EventId);
        }

        var ranking = request.RankingId == null
            ? null
            : await _rankings.Read(x => x.EventId == request.EventId && x.Id == request.RankingId.Value);
        return PdfFileNameHelper.RanklistPdf(request.RankingId ?? request.EventId, ranking?.Name);
    }
}
