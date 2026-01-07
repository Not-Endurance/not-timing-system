using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using NTS.Application.Models;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Features.Nexus;

public class InquiryBehind : ObservableBehind, IInquiryBehind
{
    readonly IRepository<ArchiveEntry> _archive;

    public InquiryBehind(IRepository<ArchiveEntry> archiveRepository)
    {
        _archive = archiveRepository;
    }

    public IEnumerable<RankingEntry>? Match { get; private set; }
    public IReadOnlyList<ArchiveModel> Records { get; private set; } = [];

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
        var archive = await _archive.ReadAll();
        Match = archive
            .SelectMany(x => x.Ranklists)
            .SelectMany(x => x.Ranking.Entries)
            .Where(x => x.Participation.Combination.Horse.Name == term)
            .OrderByDescending(x => x.Participation.Phases.First().StartTime?.ToDateTimeOffset());
        EmitChange();
    }
}
