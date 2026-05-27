using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Not.Injection;
using NTS.Application.Contracts.Pdf;
using static Not.Application.HTTP.HttpHelper;

namespace NTS.Nexus.HTTP.Functions.Pdf;

public interface IPdfPrintUrlFactory : ITransient
{
    Uri Create(PdfDocumentRequest request);
}

public class PdfPrintUrlFactory : IPdfPrintUrlFactory
{
    readonly PdfSettings _settings;

    public PdfPrintUrlFactory(IOptions<PdfSettings> settings)
    {
        _settings = settings.Value;
    }

    public Uri Create(PdfDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(_settings.PrintBaseUrl))
        {
            throw new InvalidOperationException("PdfSettings.PrintBaseUrl is required to generate PDFs.");
        }

        var path = request.Type == PdfDocumentType.Handouts ? "print/handouts" : "print/ranklist";
        var url = $"{NormalizeUri(_settings.PrintBaseUrl)}/{path}";
        var query = new Dictionary<string, string?>
        {
            ["eventId"] = request.EventId.ToString(CultureInfo.InvariantCulture),
            ["fontScale"] = request.FontScale.ToString(CultureInfo.InvariantCulture),
        };

        if (request.Type == PdfDocumentType.Handouts)
        {
            query["paper"] = request.PaperFormat.ToString();
            query["orientation"] = request.Orientation.ToString();
        }
        else
        {
            query["rankingId"] = request.RankingId?.ToString(CultureInfo.InvariantCulture);
        }

        return new Uri(QueryHelpers.AddQueryString(url, query));
    }
}
