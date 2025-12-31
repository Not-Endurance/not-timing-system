using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Injection;
using NTS.Application.Models;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Nexus.Repositories;

public class ArchiveHttpRepository : HTTP.ArchiveHttpRepository, IArchiveRepository
{
    readonly NHttpClient _client;

    public ArchiveHttpRepository(NHttpClient client)
        : base(client)
    {
        _client = client;
    }

    public async Task<IEnumerable<ArchiveModel>> SearchByHorse(int id)
    {
        var result = await _client.GetJson<IEnumerable<ArchiveModel>>($"archive/horse/{id}");
        return result ?? [];
    }
}

public interface IArchiveRepository : HTTP.IArchiveRepository, IRepository<ArchiveEntry>, ITransient
{
    Task<IEnumerable<ArchiveModel>> SearchByHorse(int id);
}
