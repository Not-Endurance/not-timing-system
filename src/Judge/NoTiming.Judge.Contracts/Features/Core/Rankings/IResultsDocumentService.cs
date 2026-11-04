using Not.Injection;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Judge.Contracts.Features.Core.Rankings;

public interface IResultsDocumentService : ITransient
{
    ResultsDocument Create(Ranking ranking);
}
