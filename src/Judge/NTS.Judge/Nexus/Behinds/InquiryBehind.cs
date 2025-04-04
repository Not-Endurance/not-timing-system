using Not.Application.Behinds.Adapters;
using Not.Async;
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

    public ParticipationDocumentModel? Match { get; private set; }
    public IReadOnlyList<ArchiveDocument> Records { get; private set; } = [];

    protected override Task<bool> PerformInitialization(params IEnumerable<object> arguments)
    {
        return Task.FromResult(true);
    }

    public async Task Search(int id)
    {
        Records = await _archiveRepository.SearchByHorse(id).ToList().AsReadonly();
        Match = Records
            .SelectMany(x => x.Ranklists)
            .SelectMany(x => x.Entries)
            .Select(x => x.Participation)
            .FirstOrDefault(x => x.Combination.Horse.Id == id);
        EmitChange();
    }

    public Task Search(string term)
    {
        throw new NotImplementedException();
    }
}
