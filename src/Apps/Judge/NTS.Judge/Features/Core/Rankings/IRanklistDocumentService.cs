using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Judge.Features.Core.Rankings;

public interface IRanklistDocumentService : IStatefulService
{
    RanklistDocument? Document { get; }
}
