using Not.Application.Behinds.Adapters;
using NTS.Application.Models;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Blazor.Nexus;

public interface IInquiryBehind : IStatefulService
{
    IEnumerable<RankingEntry>? Match { get; }
    IReadOnlyList<ArchiveModel> Records { get; }
    Task Search(int id);
    Task Search(string term);
}
