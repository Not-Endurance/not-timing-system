using Not.Files;
using Not.Injection;

namespace Not.Print;

public interface INPrintService : ITransient
{
    Task<NFileContent> CreatePdf(
        NPrintDocumentRequest request,
        CancellationToken cancellationToken = default
    );

    Task<NFileContent> CreateZip(
        NPrintBatchRequest request,
        CancellationToken cancellationToken = default
    );

    Task PrintPdf(
        NPrintDocumentRequest request,
        CancellationToken cancellationToken = default
    );

    Task DownloadPdf(
        NPrintDocumentRequest request,
        CancellationToken cancellationToken = default
    );

    Task DownloadZip(
        NPrintBatchRequest request,
        CancellationToken cancellationToken = default
    );

    Task DownloadFile(NFileContent file, CancellationToken cancellationToken = default);
}
