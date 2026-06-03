using Not.Application.HTTP;
using Not.Files;
using Not.Injection;
using Not.Print;

namespace Not.Application.Print;

public interface INPrintApiService : ITransient
{
    Task<NFileContent> CreatePdf(
        NPrintDocumentRequest request,
        CancellationToken cancellationToken = default
    );

    Task<NFileContent> CreateZip(
        NPrintBatchRequest request,
        CancellationToken cancellationToken = default
    );
}

public class NApiPrintService : INPrintApiService
{
    readonly NHttpClient _client;

    public NApiPrintService(NHttpClient client)
    {
        _client = client;
    }

    public Task<NFileContent> CreatePdf(
        NPrintDocumentRequest request,
        CancellationToken cancellationToken = default
    )
    {
        return Post(
            "print/pdf",
            request,
            NFileContentTypes.Pdf,
            EnsureExtension(request.FileName, ".pdf"),
            cancellationToken
        );
    }

    public Task<NFileContent> CreateZip(
        NPrintBatchRequest request,
        CancellationToken cancellationToken = default
    )
    {
        return Post(
            "print/zip",
            request,
            NFileContentTypes.Zip,
            EnsureExtension(request.FileName, ".zip"),
            cancellationToken
        );
    }

    async Task<NFileContent> Post(
        string endpoint,
        object payload,
        string contentType,
        string fallbackName,
        CancellationToken cancellationToken
    )
    {
        var response = await _client.PostContent(endpoint, payload, cancellationToken);
        var fileName = string.IsNullOrWhiteSpace(response.FileName) ? fallbackName : response.FileName;
        var resolvedContentType = string.IsNullOrWhiteSpace(response.ContentType) ? contentType : response.ContentType;
        return new NFileContent(fileName, resolvedContentType, response.Content);
    }

    static string EnsureExtension(string fileName, string extension)
    {
        return fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"{fileName}{extension}";
    }
}
