using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Features.Inquiry;

public class InquiryService : NStatefulService, IInquiryService
{
    readonly IRepository<ArchiveEntry> _archive;

    public InquiryService(IRepository<ArchiveEntry> archiveRepository)
    {
        _archive = archiveRepository;
    }

    public IEnumerable<RankingEntry>? Match { get; private set; }
    public IReadOnlyList<ArchiveEntryModel> Records { get; private set; } = [];

    protected override Task<bool> InitializeState()
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
        var archive = await _archive.ReadMany();
        Match = archive
            .SelectMany(x => x.Ranklists)
            .SelectMany(x => x.Ranking.Entries)
            .Where(x => x.Participation.Combination.Horse.Name == term)
            .OrderByDescending(x => x.Participation.Phases.First().StartTime?.ToDateTimeOffset());
        EmitChanged();
    }
}
