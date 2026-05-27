using Not.Injection;
using NTS.Application.Contracts.Pdf;

namespace NTS.Nexus.HTTP.Functions.Pdf;

public interface IPdfRequestValidator : ITransient
{
    string[] Validate(PdfDocumentRequest request);
    string[] Validate(PdfResultsZipRequest request);
}

public class PdfRequestValidator : IPdfRequestValidator
{
    public string[] Validate(PdfDocumentRequest request)
    {
        var errors = new List<string>();
        if (request.EventId <= 0)
        {
            errors.Add("Event id is required.");
        }
        if (request.FontScale <= 0)
        {
            errors.Add("Font scale must be greater than zero.");
        }
        if (request.Type == PdfDocumentType.Ranklist && request.RankingId is null or <= 0)
        {
            errors.Add("Ranking id is required for ranklist PDF generation.");
        }

        return errors.ToArray();
    }

    public string[] Validate(PdfResultsZipRequest request)
    {
        var errors = new List<string>();
        if (request.EventId <= 0)
        {
            errors.Add("Event id is required.");
        }
        if (request.FontScale <= 0)
        {
            errors.Add("Font scale must be greater than zero.");
        }

        return errors.ToArray();
    }
}
