using NTS.Application.Contracts.Pdf;

namespace NTS.Judge.Blazor.Features.Print;

public interface IJudgePdfBrowserService
{
    Task PrintPdf(PdfGeneratedFile file);
    Task Download(PdfGeneratedFile file);
}
