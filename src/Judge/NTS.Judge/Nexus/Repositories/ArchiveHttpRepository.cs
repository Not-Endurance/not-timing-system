using Not.Application.HTTP;
using Not.Injection;
using NTS.Storage.Documents.Archive;

namespace NTS.Judge.Nexus.Repositories;

public class ArchiveHttpRepository : IArchiveRepository
{
    readonly NHttpClient _client;

    public ArchiveHttpRepository(NHttpClient client)
    {
        _client = client;
    }

    public async Task<IEnumerable<ArchiveDocument>> SearchByHorse(int id)
    {
        var result = await _client.GetJson<IEnumerable<ArchiveDocument>>($"archive/horse/{id}");
        return result ?? [];
    }
}

public interface IArchiveRepository : ITransient
{
    Task<IEnumerable<ArchiveDocument>> SearchByHorse(int id);
}
