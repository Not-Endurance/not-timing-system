using Not.Files;
using Not.Injection;

namespace Not.Print;

public interface INPrintService : ITransient
{
    Task<NFile> CreatePdf(NPrintDocumentRequest request, CancellationToken cancellationToken = default);

    Task<NFile> CreateZip(NPrintBatchRequest request, CancellationToken cancellationToken = default);

    Task PrintPdf(NPrintDocumentRequest request, CancellationToken cancellationToken = default);

    Task DownloadPdf(NPrintDocumentRequest request, CancellationToken cancellationToken = default);

    Task DownloadZip(NPrintBatchRequest request, CancellationToken cancellationToken = default);

    Task DownloadFile(NFile file, CancellationToken cancellationToken = default);
}
