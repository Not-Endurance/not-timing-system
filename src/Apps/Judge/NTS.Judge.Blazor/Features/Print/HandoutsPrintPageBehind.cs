using NTS.Application.Contracts.Pdf;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Judge.Blazor.Features.Print;

public class HandoutsPrintPageBehind : ComponentBase
{
    [Inject]
    IJudgePrintDocumentService PrintDocumentService { get; set; } = default!;

    protected IReadOnlyList<HandoutDocument> Documents { get; private set; } = [];
    protected string? ErrorMessage { get; private set; }
    protected bool IsReady { get; private set; }

    [SupplyParameterFromQuery]
    public int EventId { get; set; }

    [SupplyParameterFromQuery]
    public decimal FontScale { get; set; } = 0.85m;

    [SupplyParameterFromQuery(Name = "paper")]
    public PdfPaperFormat PaperFormat { get; set; } = PdfPaperFormat.A5;

    [SupplyParameterFromQuery]
    public PdfOrientation Orientation { get; set; } = PdfOrientation.Landscape;

    protected override async Task OnParametersSetAsync()
    {
        try
        {
            IsReady = false;
            ErrorMessage = null;
            Documents = await PrintDocumentService.CreateHandouts(EventId);
            IsReady = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
