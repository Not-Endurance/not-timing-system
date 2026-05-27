using Not.Injection;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Judge.Contracts.Features.Print;

public interface IJudgePrintDocumentService : ITransient
{
    Task<IReadOnlyList<HandoutDocument>> CreateHandouts(int eventId);
    Task<ProtocolDocument?> CreateRanklist(int eventId, int rankingId);
}
