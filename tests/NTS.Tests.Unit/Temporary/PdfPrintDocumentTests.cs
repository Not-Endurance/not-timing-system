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
        Assert.DoesNotContain("--print-spacing-scale", css);
        Assert.Contains("font-size: calc(16px * var(--print-font-scale));", css);
        Assert.Contains(".print-document", css);
        Assert.Contains(".results-print-page", css);
    }

    [Fact]
    public void ResultsPrintComponents_UseResponsiveLayoutWithoutHorizontalOrCompactParameters()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tableSource = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "NTS.Blazor",
                "Components",
                "ParticipationTable",
                "ParticipationTable.razor"
            )
        );
        var tableBehindSource = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "NTS.Blazor",
                "Components",
                "ParticipationTable",
                "ParticipationTableBehind.cs"
            )
        );
        var resultsPrintDocumentSource = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "NTS.Blazor",
                "Components",
                "Print",
                "ResultsPrintDocument.razor"
            )
        );
        var resultsPrintDocumentBehindSource = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "NTS.Blazor",
                "Components",
                "Print",
                "ResultsPrintDocumentBehind.cs"
            )
        );
        var resultsDocumentViewSource = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "NTS.Blazor",
                "Components",
                "Results",
                "ResultsDocumentView.razor"
            )
        );
        var resultsDocumentViewBehindSource = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "NTS.Blazor",
                "Components",
                "Results",
                "ResultsDocumentViewBehind.cs"
            )
        );
        var resultsRowSource = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "NTS.Blazor",
                "Components",
                "Results",
                "ResultsDocumentRow.razor"
            )
        );
        var resultsRowBehindSource = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "NTS.Blazor",
                "Components",
                "Results",
                "ResultsDocumentRowBehind.cs"
            )
        );
        var resultsSummarySource = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "NTS.Blazor",
                "Components",
                "Results",
                "ResultsRowSummary.razor"
            )
        );
        var resultsSummaryBehindSource = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "NTS.Blazor",
                "Components",
                "Results",
                "ResultsRowSummaryBehind.cs"
            )
        );
        var componentSource = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "nugets",
                "Not.Blazor",
                "Components",
                "Abstractions",
                "NComponent.cs"
            )
        );

        Assert.Contains("padding-block: 4px !important;", tableSource);
        Assert.Contains("padding-inline: 8px !important;", tableSource);
        Assert.Contains("@media print", tableSource);
        Assert.Contains("padding-block: 0 !important;", tableSource);
        Assert.Contains("UseHorizontalLayout", tableSource);
        Assert.Contains("protected override bool ObserveBreakpointChanges => true;", tableBehindSource);
        Assert.Contains("UseHorizontalLayout => !IsMdAndDown", tableBehindSource);
        Assert.Contains("protected bool IsMdAndDown", componentSource);
        Assert.Contains("ViewportService.SubscribeAsync", componentSource);
        Assert.DoesNotContain("--print-spacing-scale", tableSource);
        Assert.DoesNotContain("participation-table-responsive", tableSource);
        Assert.DoesNotContain("participation-table-compact", tableSource);
        Assert.DoesNotContain("Compact", tableBehindSource);
        Assert.DoesNotContain("public bool Horizontal", tableBehindSource);
        Assert.DoesNotContain("Horizontal=\"", resultsRowSource);
        Assert.DoesNotContain("Horizontal", resultsPrintDocumentSource);
        Assert.DoesNotContain("Horizontal", resultsPrintDocumentBehindSource);
        Assert.DoesNotContain("Horizontal", resultsDocumentViewSource);
        Assert.DoesNotContain("Horizontal", resultsDocumentViewBehindSource);
        Assert.DoesNotContain("public bool Horizontal", resultsRowBehindSource);
        Assert.DoesNotContain("Horizontal", resultsSummarySource);
        Assert.DoesNotContain("public bool Horizontal", resultsSummaryBehindSource);
        Assert.Contains("protected override bool ObserveBreakpointChanges => true;", resultsRowBehindSource);
        Assert.Contains("UseInlineLayout => !IsMdAndDown", resultsRowBehindSource);
        Assert.Contains("protected override bool ObserveBreakpointChanges => true;", resultsSummaryBehindSource);
        Assert.Contains("UseInlineLayout => !IsMdAndDown", resultsSummaryBehindSource);
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

    static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
