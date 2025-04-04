using Not.Application.Behinds.Adapters;
using Not.Async;
using NTS.Domain.Core.Aggregates;
using NTS.Judge.Blazor.Nexus;
using NTS.Judge.Nexus.Repositories;
using NTS.Storage.Documents.Archive;
using NTS.Storage.Documents.Archive.Models;

namespace NTS.Judge.Nexus.Behinds;

public class InquiryBehind : ObservableBehind, IInquiryBehind
{
    readonly IArchiveRepository _archiveRepository;

    public InquiryBehind(IArchiveRepository archiveRepository)
    {
        _archiveRepository = archiveRepository;
    }

    public IEnumerable<RankingEntry>? Match { get; private set; }
    public IReadOnlyList<ArchiveDocument> Records { get; private set; } = [];

    protected override Task<bool> PerformInitialization(params IEnumerable<object> arguments)
    {
        return Task.FromResult(true);
    }

    public Task Search(int id)
    {
        return Task.CompletedTask;
        //Records = await _archiveRepository.SearchByHorse(id).ToList().AsReadonly();
        //Match = Records
        //    .SelectMany(x => x.Ranklists)
        //    .SelectMany(x => x.Entries)
        //    .Select(x => x.Participation)
        //    .Where(x => x.Combination.Horse.Id == id)
        //    .ToList();
        //EmitChange();
    }

    public async Task Search(string term)
    {
        var archive = await _archiveRepository.GetEntries();
        Match = archive
            .SelectMany(x => x.Ranklists)
            .SelectMany(x => x.Ranking.Entries)
            .Where(x => x.Participation.Combination.Horse.Name == term)
            .OrderByDescending(x => x.Participation.Phases.First().StartTime?.ToDateTimeOffset());
        EmitChange();
    }
}
