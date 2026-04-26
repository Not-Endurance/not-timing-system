using Not.Application.Behinds.Adapters;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Contracts.Features.Inquiry;

public interface IInquiryService : IStatefulService
{
    IEnumerable<RankingEntry>? Match { get; }
    IReadOnlyList<ArchiveEntryModel> Records { get; }
    Task Search(int id);
    Task Search(string term);
}
