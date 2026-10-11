using System.Text;
using Not.Blazor.Components.Print;
using Not.Print;
using NoTiming.Ui.Components.Results;
using NTS.Contracts;
using NTS.Domain.Core.Objects.Documents;

namespace NoTiming.Console.Features.Printing;

/// <summary>
/// What the Console prints through the browser (#646, ADR-0011): the Results of a Ranking and the Handouts of an Event as the
/// HTML of their documents, in the page that the person chose (its size, its direction, its scale), with the logos of the Tenant
/// at the head of each. The browser prints that page and the person saves it as a file there, if they want one: nothing is
/// rendered on a server. The logos are the Tenant's, or the Event's when it has its own.
/// </summary>
public sealed class PrintRequests
{
    const string HANDOUT_PAGE_MARGIN = "6mm 6mm 10mm 6mm";
    const string RANKLIST_PAGE_MARGIN = "10mm 10mm 12mm 10mm";
    const string BACKDROP = "appicon.svg";

    readonly INHtmlComponentRenderer _renderer;

    public PrintRequests(INHtmlComponentRenderer renderer)
    {
        _renderer = renderer;
    }

    static string GeneratedByText => $"{Generated_by_NoTiming_System_v_string}{ApplicationConstants.VERSION}";

    public async Task<NPrintDocumentRequest> Ranklist(
        ResultsDocument document,
        NPrintPanelContext context,
        string? leftLogo,
        string? rightLogo
    )
    {
        return new NPrintDocumentRequest
        {
            Title = Ranklist_string,
            FileName = $"{document.Header.Title}.pdf",
            Html = await RenderResult(document, leftLogo, rightLogo),
            FooterText = GeneratedByText,
            BackdropImage = BACKDROP,
            Page = new NPrintPageOptions
            {
                PaperFormat = NPrintPaperFormat.A4,
                Orientation = NPrintOrientation.Portrait,
                Scale = context.Scale,
                Margin = RANKLIST_PAGE_MARGIN,
            },
        };
    }

    public async Task<NPrintDocumentRequest> Handouts(
        IReadOnlyList<ResultsDocument> documents,
        NPrintPanelContext context,
        string? leftLogo,
        string? rightLogo
    )
    {
        var html = new StringBuilder();
        foreach (var document in documents)
        {
            if (html.Length > 0)
            {
                html.AppendLine();
            }

            html.Append(await RenderResult(document, leftLogo, rightLogo));
        }

        return new NPrintDocumentRequest
        {
            Title = Handouts_string,
            FileName = "handouts.pdf",
            Html = html.ToString(),
            FooterText = GeneratedByText,
            BackdropImage = BACKDROP,
            Page = new NPrintPageOptions
            {
                PaperFormat = context.PaperFormat,
                Orientation = context.Orientation,
                Scale = context.Scale,
                Margin = HANDOUT_PAGE_MARGIN,
            },
        };
    }

    async Task<string> RenderResult(ResultsDocument document, string? leftLogo, string? rightLogo)
    {
        return await _renderer.Render<ResultComponent>(
            new Dictionary<string, object?>
            {
                [nameof(ResultComponent.Document)] = document,
                [nameof(ResultComponent.LeftLogo)] = leftLogo,
                [nameof(ResultComponent.RightLogo)] = rightLogo,
            }
        );
    }
}
