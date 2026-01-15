using Not.Blazor.Ports;
using Not.Injection;
using Not.Startup;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Judge.Features.Core.Handouts;

public interface IHandoutsBehind : IStartupInitializer, INObservable, ISingleton
{
    IReadOnlyList<HandoutDocument> Documents { get; }
    Task Delete(IEnumerable<HandoutDocument> documents);
}
