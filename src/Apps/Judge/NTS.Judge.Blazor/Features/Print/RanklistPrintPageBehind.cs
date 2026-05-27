using NTS.Application.Contracts.Pdf;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Judge.Blazor.Features.Print;

public class RanklistPrintPageBehind : ComponentBase
{
    [Inject]
    IJudgePrintDocumentService PrintDocumentService { get; set; } = default!;

    protected ProtocolDocument? Document { get; private set; }
    protected string? ErrorMessage { get; private set; }
    protected bool IsReady { get; private set; }

    [SupplyParameterFromQuery]
    public int EventId { get; set; }

    [SupplyParameterFromQuery]
    public int RankingId { get; set; }

    [SupplyParameterFromQuery]
    public decimal FontScale { get; set; } = 0.8m;

    protected override async Task OnParametersSetAsync()
    {
        try
        {
            IsReady = false;
            ErrorMessage = null;
            Document = await PrintDocumentService.CreateRanklist(EventId, RankingId);
            if (Document == null)
            {
                ErrorMessage = $"Ranklist '{RankingId}' was not found for event '{EventId}'.";
                return;
            }
            IsReady = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
