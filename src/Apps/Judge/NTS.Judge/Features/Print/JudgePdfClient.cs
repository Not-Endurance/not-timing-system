using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Not.Application.HTTP;
using Not.Serialization.JSON;
using NTS.Application.Contracts.Pdf;
using NTS.Judge.Contracts.Features.Print;

namespace NTS.Judge.Features.Print;

public class JudgePdfClient : IJudgePdfClient
{
    readonly IHttpClientFactory _httpClientFactory;
    readonly ILogger<JudgePdfClient> _logger;
    readonly NHttpSettings _settings;

    public JudgePdfClient(
        IHttpClientFactory httpClientFactory,
        IOptions<NHttpSettings> settings,
        ILogger<JudgePdfClient> logger
    )
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public Task<PdfGeneratedFile> CreatePdf(PdfDocumentRequest request)
    {
        var fallbackName = request.Type == PdfDocumentType.Handouts
            ? PdfFileNameHelper.HandoutsPdf(request.EventId)
            : PdfFileNameHelper.RanklistPdf(request.RankingId ?? request.EventId, null);
        return Post("pdf", request, "application/pdf", fallbackName);
    }

    public Task<PdfGeneratedFile> CreateResultsZip(PdfResultsZipRequest request)
    {
        return Post("pdf/results", request, "application/zip", PdfFileNameHelper.ResultsZip(request.EventId));
    }

    async Task<PdfGeneratedFile> Post(string endpoint, object payload, string contentType, string fallbackName)
    {
        var url = BuildUrl(endpoint);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(payload.ToJson(), Encoding.UTF8, "application/json");

            using var client = _httpClientFactory.CreateClient(nameof(JudgePdfClient));
            using var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsByteArrayAsync();
            if (!response.IsSuccessStatusCode)
            {
                var message = Encoding.UTF8.GetString(content);
                throw new HttpRequestException(
                    $"POST {url} failed with status code {(int)response.StatusCode} ({response.ReasonPhrase}). {message}",
                    null,
                    response.StatusCode
                );
            }

            var fileName = ResolveFileName(response.Content.Headers.ContentDisposition, fallbackName);
            var resolvedContentType = response.Content.Headers.ContentType?.MediaType ?? contentType;
            return new PdfGeneratedFile(fileName, resolvedContentType, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during PDF request to {Url}", url);
            throw;
        }
    }

    Uri BuildUrl(string endpoint)
    {
        var baseUrl = _settings.Url;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("NHttpSettings.Url is required to generate PDFs.");
        }

        return new Uri($"{HttpHelper.NormalizeUri(baseUrl)}/{HttpHelper.NormalizeUri(endpoint)}");
    }

    static string ResolveFileName(ContentDispositionHeaderValue? contentDisposition, string fallbackName)
    {
        var fileName = contentDisposition?.FileNameStar ?? contentDisposition?.FileName;
        return string.IsNullOrWhiteSpace(fileName) ? fallbackName : fileName.Trim('"');
    }
}
