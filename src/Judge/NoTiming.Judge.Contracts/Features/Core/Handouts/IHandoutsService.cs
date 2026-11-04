using Not.Application.Behinds.Adapters;
using Not.Injection;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Judge.Contracts.Features.Core.Handouts;

public interface IHandoutsService : IStatefulService, IScoped
{
    IReadOnlyList<ResultsDocument> Documents { get; }
    Task Delete(IEnumerable<ResultsDocument> documents);
}
