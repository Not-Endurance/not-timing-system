using Not.Blazor.Ports;
using Not.Injection;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Judge.Blazor.Core.Rankings;

public interface IRankingDocumentBehind : IObservableBehind, ISingleton
{
    Task<RanklistDocument> CreateDocument();
}
