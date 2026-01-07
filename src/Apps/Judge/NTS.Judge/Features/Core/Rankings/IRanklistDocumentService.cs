using Not.Blazor.Ports;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Judge.Features.Core.Rankings;

public interface IRanklistDocumentService : INObservable
{
    RanklistDocument? Document { get; }
}
