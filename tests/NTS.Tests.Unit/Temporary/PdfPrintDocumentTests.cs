using Not.Files;
using Not.Print;
using Not.Server.Print;

namespace NTS.Tests.Unit.Temporary;

public class PdfPrintDocumentTests
{
    [Fact]
    public void NPrintDocumentRequest_UsesGenericTemplateAndPageDefaults()
    {
        var request = new NPrintDocumentRequest { FileName = "ranklist.pdf", Html = "<main />" };

        Assert.Equal(NPrintTemplateIds.Default, request.TemplateId);
        Assert.Equal(NPrintPaperFormat.A4, request.Page.PaperFormat);
        Assert.Equal(NPrintOrientation.Portrait, request.Page.Orientation);
        Assert.Equal(1m, request.Page.Scale);
        Assert.Equal("10mm", request.Page.Margin);
    }

    [Fact]
    public void NFileContentTypes_ResolveCommonFormats()
    {
        Assert.Equal(NFileContentTypes.Png, NFileContentTypes.FromFileName("logo.png"));
        Assert.Equal(NFileContentTypes.Jpeg, NFileContentTypes.FromFileName("logo.jpeg"));
        Assert.Equal(NFileContentTypes.Csv, NFileContentTypes.FromFileName("results.csv"));
        Assert.Equal(NFileContentTypes.Binary, NFileContentTypes.FromFileName("archive.unknown"));
    }

    [Fact]
    public void NPrintRequestValidator_RequiresGenericTemplateAndSafeNames()
    {
        var validator = new NPrintRequestValidator();
        var request = new NPrintDocumentRequest
        {
            TemplateId = "nts-ranklist",
            FileName = "../ranklist.pdf",
            Html = "",
            Page = new NPrintPageOptions { Scale = 0 },
        };

        var errors = validator.Validate(request);

        Assert.Contains(errors, x => x.Contains("Unsupported print template"));
        Assert.Contains(errors, x => x.Contains("safe PDF file name"));
        Assert.Contains(errors, x => x.Contains("Print HTML is required"));
        Assert.Contains(errors, x => x.Contains("Print scale"));
    }

    [Fact]
    public void NPrintDocumentCss_UsesRequestedPageAndScale()
    {
        var css = NPrintDocumentCss.Create(
            new NPrintPageOptions
            {
                PaperFormat = NPrintPaperFormat.A5,
                Orientation = NPrintOrientation.Landscape,
                Scale = 0.85m,
                Margin = "6mm",
            }
        );

        Assert.Contains("size: A5 landscape;", css);
        Assert.Contains("margin: 6mm;", css);
        Assert.Contains("--print-font-scale: 0.85;", css);
        Assert.Contains("font-size: calc(16px * var(--print-font-scale));", css);
        Assert.Contains(".print-document", css);
        Assert.Contains(".handout-print-page", css);
    }

    [Fact]
    public void NPrintDocumentCss_UsesNamedPaperFormats()
    {
        var css = NPrintDocumentCss.Create(
            new NPrintPageOptions
            {
                PaperFormat = NPrintPaperFormat.Letter,
                Orientation = NPrintOrientation.Portrait,
            }
        );

        Assert.Contains("size: Letter portrait;", css);
    }
}
