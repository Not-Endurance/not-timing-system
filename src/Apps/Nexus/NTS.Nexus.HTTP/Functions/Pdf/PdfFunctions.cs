using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Files;
using Not.Print;
using Not.Server.Print;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions.Pdf;

public class PdfFunctions : FunctionBase
{
    readonly INPrintService _print;
    readonly INPrintRequestValidator _validator;

    public PdfFunctions(
        IFunctionLogger<PdfFunctions> logger,
        ITelemetryService telemetry,
        INPrintService print,
        INPrintRequestValidator validator
    )
        : base(logger, telemetry)
    {
        _print = print;
        _validator = validator;
    }

    [Function("print-pdf-create")]
    public async Task<IActionResult> CreatePdf(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "print/pdf")] HttpRequest request,
        CancellationToken cancellationToken
    )
    {
        using var activity = StartFunctionActivity(nameof(CreatePdf));
        TagRequest(request);
        LogInformation(request, nameof(CreatePdf));

        try
        {
            var payload = await ReadBody<NPrintDocumentRequest>(request);
            var errors = _validator.Validate(payload);
            if (errors.Length != 0)
            {
                return InvalidPayload(string.Join(Environment.NewLine, errors));
            }

            var file = await _print.CreatePdf(payload, cancellationToken);
            return File(file);
        }
        catch (Exception ex)
        {
            LogError(request, ex, nameof(CreatePdf));
            return Error(ex);
        }
    }

    [Function("print-zip-create")]
    public async Task<IActionResult> CreateZip(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "print/zip")] HttpRequest request,
        CancellationToken cancellationToken
    )
    {
        using var activity = StartFunctionActivity(nameof(CreateZip));
        TagRequest(request);
        LogInformation(request, nameof(CreateZip));

        try
        {
            var payload = await ReadBody<NPrintBatchRequest>(request);
            var errors = _validator.Validate(payload);
            if (errors.Length != 0)
            {
                return InvalidPayload(string.Join(Environment.NewLine, errors));
            }

            var file = await _print.CreateZip(payload, cancellationToken);
            return File(file);
        }
        catch (Exception ex)
        {
            LogError(request, ex, nameof(CreateZip));
            return Error(ex);
        }
    }

    static FileContentResult File(NFileContent file)
    {
        return new FileContentResult(file.Content, file.ContentType) { FileDownloadName = file.FileName };
    }

    static ObjectResult Error(Exception exception)
    {
        return new ObjectResult(exception.Message) { StatusCode = StatusCodes.Status500InternalServerError };
    }
}
