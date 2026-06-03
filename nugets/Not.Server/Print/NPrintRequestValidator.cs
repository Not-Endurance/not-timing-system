using Not.Files;
using Not.Print;

namespace Not.Server.Print;

public sealed class NPrintRequestValidator : INPrintRequestValidator
{
    public string[] Validate(NPrintDocumentRequest request)
    {
        var errors = new List<string>();
        ValidateDocument(request, errors, requirePdfFileName: true);
        return errors.ToArray();
    }

    public string[] Validate(NPrintBatchRequest request)
    {
        var errors = new List<string>();
        if (!NFileNameHelper.IsSafeFileName(request.FileName) || !HasExtension(request.FileName, ".zip"))
        {
            errors.Add("A safe ZIP file name is required.");
        }
        if (request.Documents.Count == 0)
        {
            errors.Add("At least one print document is required.");
        }
        foreach (var document in request.Documents)
        {
            ValidateDocument(document, errors, requirePdfFileName: true);
        }

        return errors.ToArray();
    }

    static void ValidateDocument(
        NPrintDocumentRequest request,
        ICollection<string> errors,
        bool requirePdfFileName
    )
    {
        if (request.TemplateId != NPrintTemplateIds.Default)
        {
            errors.Add($"Unsupported print template '{request.TemplateId}'.");
        }
        if (
            !NFileNameHelper.IsSafeFileName(request.FileName)
            || (requirePdfFileName && !HasExtension(request.FileName, ".pdf"))
        )
        {
            errors.Add("A safe PDF file name is required.");
        }
        if (string.IsNullOrWhiteSpace(request.Html))
        {
            errors.Add("Print HTML is required.");
        }
        if (request.Page.Scale <= 0)
        {
            errors.Add("Print scale must be greater than zero.");
        }
        if (string.IsNullOrWhiteSpace(request.Page.Margin))
        {
            errors.Add("Print margin is required.");
        }
    }

    static bool HasExtension(string fileName, string extension)
    {
        return fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
    }
}
