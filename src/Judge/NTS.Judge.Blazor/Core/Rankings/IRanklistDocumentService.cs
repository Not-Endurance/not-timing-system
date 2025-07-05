using Not.Blazor.Ports;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Judge.Blazor.Core.Rankings;

public interface IRanklistDocumentService : IObservableBehind
{
    RanklistDocument? Document { get; }
}
