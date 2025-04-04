using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Injection;
using NTS.Domain.Core.Aggregates;
using NTS.Judge.HTTP;
using NTS.Storage.Documents.Archive;

namespace NTS.Judge.Nexus.Repositories;

public class ArchiveHttpRepository : NTS.Judge.HTTP.ArchiveHttpRepository, IArchiveRepository
{
    readonly NHttpClient _client;

    public ArchiveHttpRepository(NHttpClient client) : base(client)
    {
        _client = client;
    }

    public async Task<IEnumerable<ArchiveDocument>> SearchByHorse(int id)
    {
        var result = await _client.GetJson<IEnumerable<ArchiveDocument>>($"archive/horse/{id}");
        return result ?? [];
    }
}

public interface IArchiveRepository : HTTP.IArchiveRepository, IRepository<ArchiveEntry>, ITransient
{
    Task<IEnumerable<ArchiveDocument>> SearchByHorse(int id);
}
