using Not.Blazor.Ports;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.Documents.Archive;

namespace NTS.Judge.Blazor.Nexus;

public interface IInquiryBehind : IObservableBehind
{
    IEnumerable<RankingEntry>? Match { get; }
    IReadOnlyList<ArchiveDocument> Records { get; }
    Task Search(int id);
    Task Search(string term);
}
