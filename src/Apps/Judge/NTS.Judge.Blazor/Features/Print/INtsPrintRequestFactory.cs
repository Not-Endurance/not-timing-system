using Not.Blazor.Components.Print;
using Not.Print;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Judge.Blazor.Features.Print;

public interface INtsPrintRequestFactory
{
    Task<NPrintDocumentRequest> CreateHandouts(
        IReadOnlyList<HandoutDocument> documents,
        NPrintPanelContext context,
        string fileName
    );

    Task<NPrintDocumentRequest> CreateRanklist(
        ProtocolDocument document,
        NPrintPanelContext context,
        string fileName,
        string? leftLogo,
        string? rightLogo
    );

    Task<NPrintBatchRequest> CreateRanklistsZip(
        IReadOnlyList<Ranking> rankings,
        Func<Ranking, ProtocolDocument> createDocument,
        NPrintPanelContext context,
        string fileName,
        string? leftLogo,
        string? rightLogo
    );
}
