using Not.Injection;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Judge.Contracts.Features.Core.Rankings;

public interface IRanklistDocumentService : ITransient
{
    RanklistDocument Create(Ranking ranking);
}
