using Not.Injection;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Judge.Features.Core.Rankings;

public interface IRanklistDocumentFactory : ITransient
{
    RanklistDocument Create(Ranking ranking);
}
