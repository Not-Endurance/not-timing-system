using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using NTS.Application.Contracts.Pdf;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions.Pdf;

public class PdfFunctions : FunctionBase
{
    readonly IPdfGenerationService _pdfs;
    readonly IPdfRequestValidator _validator;

    public PdfFunctions(
        IFunctionLogger<PdfFunctions> logger,
        ITelemetryService telemetry,
        IPdfGenerationService pdfs,
        IPdfRequestValidator validator
    )
        : base(logger, telemetry)
    {
        _pdfs = pdfs;
        _validator = validator;
    }

    [Function("pdf-create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "pdf")] HttpRequest request,
        CancellationToken cancellationToken
    )
    {
        using var activity = StartFunctionActivity(nameof(Create));
        TagRequest(request);
        LogInformation(request, nameof(Create));

        var payload = await ReadBody<PdfDocumentRequest>(request);
        var errors = _validator.Validate(payload);
        if (errors.Length != 0)
        {
            return InvalidPayload(string.Join(Environment.NewLine, errors));
        }

        var file = await _pdfs.Create(payload, cancellationToken);
        return File(file);
    }

    [Function("pdf-create-results")]
    public async Task<IActionResult> CreateResults(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "pdf/results")] HttpRequest request,
        CancellationToken cancellationToken
    )
    {
        using var activity = StartFunctionActivity(nameof(CreateResults));
        TagRequest(request);
        LogInformation(request, nameof(CreateResults));

        var payload = await ReadBody<PdfResultsZipRequest>(request);
        var errors = _validator.Validate(payload);
        if (errors.Length != 0)
        {
            return InvalidPayload(string.Join(Environment.NewLine, errors));
        }

        var file = await _pdfs.CreateResultsZip(payload, cancellationToken);
        return File(file);
    }

    static FileContentResult File(PdfGeneratedFile file)
    {
        return new FileContentResult(file.Content, file.ContentType) { FileDownloadName = file.FileName };
    }
}
