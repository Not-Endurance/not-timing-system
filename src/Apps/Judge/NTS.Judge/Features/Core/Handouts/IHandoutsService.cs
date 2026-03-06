using Not.Application.Behinds.Adapters;
using Not.Injection;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Judge.Features.Core.Handouts;

public interface IHandoutsService : IStatefulService, ISingleton
{
    IReadOnlyList<HandoutDocument> Documents { get; }
    Task Delete(IEnumerable<HandoutDocument> documents);
}
