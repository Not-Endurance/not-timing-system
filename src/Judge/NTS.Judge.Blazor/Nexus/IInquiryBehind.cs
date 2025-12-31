using Not.Blazor.Ports;
using NTS.Application.Models;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Blazor.Nexus;

public interface IInquiryBehind : IObservableBehind
{
    IEnumerable<RankingEntry>? Match { get; }
    IReadOnlyList<ArchiveModel> Records { get; }
    Task Search(int id);
    Task Search(string term);
}
