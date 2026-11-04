using NC.Injection;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Objects.Documents;

namespace NoTiming.Judge.Contracts.Features.Core.Rankings;

public interface IResultsDocumentService : ITransient
{
    ResultsDocument Create(Ranking ranking);
}
