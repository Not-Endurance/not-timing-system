using Not.Blazor.Ports;
using NTS.Application.Models;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Features.Nexus;

public interface IInquiryBehind : IStatefulService
{
    IEnumerable<RankingEntry>? Match { get; }
    IReadOnlyList<ArchiveModel> Records { get; }
    Task Search(int id);
    Task Search(string term);
}
