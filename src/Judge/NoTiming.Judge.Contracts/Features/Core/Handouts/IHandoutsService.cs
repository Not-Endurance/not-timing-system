using NC.Application.Behinds.Adapters;
using NC.Injection;
using NoTiming.Domain.Core.Objects.Documents;

namespace NoTiming.Judge.Contracts.Features.Core.Handouts;

public interface IHandoutsService : IStatefulService, IScoped
{
    IReadOnlyList<ResultsDocument> Documents { get; }
    Task Delete(IEnumerable<ResultsDocument> documents);
}
