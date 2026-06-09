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
        Assert.Null(request.FooterText);
        Assert.Null(request.BackdropImage);
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
        Assert.Contains(".print-backdrop", css);
        Assert.Contains(".print-footer", css);
        Assert.Contains(".results-print-section", css);
        Assert.Contains(".results-print-page", css);
    }

    [Fact]
    public void NPrintTemplateRenderer_RendersFooterAndBackdropFromRequest()
    {
        var renderer = new NPrintTemplateRenderer();
        var html = renderer.Render(
            new NPrintDocumentRequest
            {
                FileName = "ranklist.pdf",
                Html = "<main />",
                FooterText = "<generated>",
                BackdropImage = "Resources/AppIcon/appicon.svg",
            }
        );

        Assert.Contains("<div class=\"print-backdrop\" aria-hidden=\"true\">", html);
        Assert.Contains("src=\"Resources/AppIcon/appicon.svg\"", html);
        Assert.Contains("<div class=\"print-footer\">&lt;generated&gt;</div>", html);
        Assert.Contains("<main />", html);
    }

    [Fact]
    public void ParticipationTable_UsesExplicitModesForDashboardAndResultDocuments()
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
        var resultComponentSource = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "NTS.Blazor", "Components", "Results", "ResultComponent.razor")
        );
        var resultComponentBehindSource = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "NTS.Blazor", "Components", "Results", "ResultComponentBehind.cs")
        );
        var participationResultSource = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "NTS.Blazor",
                "Components",
                "Results",
                "ParticipationResultComponent.razor"
            )
        );
        var dashboardSource = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "Apps",
                "Judge",
                "NTS.Judge.Blazor",
                "Features",
                "Core",
                "Dashboards",
                "Components",
                "Dashboard.razor"
            )
        );
        var participationResultBehindSource = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "NTS.Blazor",
                "Components",
                "Results",
                "ParticipationResultComponentBehind.cs"
            )
        );
        var participationSummarySource = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "NTS.Blazor",
                "Components",
                "Results",
                "ParticipationSummaryComponent.razor"
            )
        );
        var participationSummaryBehindSource = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "NTS.Blazor",
                "Components",
                "Results",
                "ParticipationSummaryComponentBehind.cs"
            )
        );
        var componentSource = File.ReadAllText(
            Path.Combine(repositoryRoot, "nugets", "Not.Blazor", "Components", "Abstractions", "NComponent.cs")
        );

        Assert.Contains("padding-block: 4px !important;", tableSource);
        Assert.Contains("padding-inline: 8px !important;", tableSource);
        Assert.Contains("@media print", tableSource);
        Assert.Contains("padding-block: 0 !important;", tableSource);
        Assert.Contains("UseHorizontalLayout", tableSource);
        Assert.Contains("protected override bool ObserveBreakpointChanges => true;", tableBehindSource);
        Assert.Contains("public ParticipationTableMode Mode { get; set; }", tableBehindSource);
        Assert.Contains("ParticipationTableMode.Horizontal => true", tableBehindSource);
        Assert.Contains("ParticipationTableMode.Vertical => false", tableBehindSource);
        Assert.Contains("_ => !IsMdAndDown", tableBehindSource);
        Assert.Contains("protected bool IsMdAndDown", componentSource);
        Assert.Contains("ViewportService.SubscribeAsync", componentSource);
        Assert.DoesNotContain("--print-spacing-scale", tableSource);
        Assert.DoesNotContain("participation-table-responsive", tableSource);
        Assert.DoesNotContain("participation-table-compact", tableSource);
        Assert.DoesNotContain("Compact", tableBehindSource);
        Assert.DoesNotContain("public bool Horizontal", tableBehindSource);
        Assert.Contains("Mode=\"ParticipationTableMode.Vertical\"", dashboardSource);
        Assert.Contains("Mode=\"ParticipationTableMode.Horizontal\"", participationResultSource);
        Assert.DoesNotContain("Horizontal", resultComponentSource);
        Assert.DoesNotContain("Horizontal", resultComponentBehindSource);
        Assert.DoesNotContain("public bool Horizontal", participationResultBehindSource);
        Assert.DoesNotContain("Horizontal", participationSummarySource);
        Assert.DoesNotContain("public bool Horizontal", participationSummaryBehindSource);
        Assert.Contains("ResultHeaderComponent", resultComponentSource);
        Assert.Contains("ParticipationResultComponent", resultComponentSource);
        Assert.Contains("protected override bool ObserveBreakpointChanges => true;", participationResultBehindSource);
        Assert.Contains("UseInlineLayout => !IsMdAndDown", participationResultBehindSource);
        Assert.Contains("protected override bool ObserveBreakpointChanges => true;", participationSummaryBehindSource);
        Assert.Contains("UseInlineLayout => !IsMdAndDown", participationSummaryBehindSource);
        Assert.False(
            File.Exists(
                Path.Combine(repositoryRoot, "src", "NTS.Blazor", "Components", "Print", "ResultsPrintDocument.razor")
            )
        );
    }

    [Fact]
    public void NPrintDocumentCss_UsesNamedPaperFormats()
    {
        var css = NPrintDocumentCss.Create(
            new NPrintPageOptions { PaperFormat = NPrintPaperFormat.Letter, Orientation = NPrintOrientation.Portrait }
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
