using Not.Injection;
using NTS.Application.Contracts.Pdf;

namespace NTS.Judge.Contracts.Features.Print;

public interface IJudgePdfClient : ITransient
{
    Task<PdfGeneratedFile> CreatePdf(PdfDocumentRequest request);
    Task<PdfGeneratedFile> CreateResultsZip(PdfResultsZipRequest request);
}
