using Not.Files;
using Not.Injection;

namespace Not.Print;

public interface INPrintService : ITransient
{
    /// <summary>Prints the document through the print dialog of the browser: nothing is sent to a server.</summary>
    Task PrintPdf(NPrintDocumentRequest request, CancellationToken cancellationToken = default);

    Task DownloadFile(NFile file, CancellationToken cancellationToken = default);
}
