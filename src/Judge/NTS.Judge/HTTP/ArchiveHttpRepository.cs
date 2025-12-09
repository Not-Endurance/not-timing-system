using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Serialization.JSON;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.HTTP;

public class ArchiveHttpRepository : HttpRepository<ArchiveEntry>, IArchiveRepository
{
    readonly NHttpClient _client;

    public ArchiveHttpRepository(NHttpClient client)
        : base("archive", client)
    {
        _client = client;
    }

    public async Task<IEnumerable<ArchiveEntry>> GetEntries()
    {
        var contents = await _client.Get("archive");
        if (contents == null)
        {
            return [];
        }
        return contents.FromJson<IEnumerable<ArchiveModel>>().Select(x => x.ToDomain());
    }

    public async Task<ArchiveEntry?> GetEntry(int id)
    {
        var contents = await _client.Get("archive");
        if (contents == null)
        {
            return null;
        }
        var documents = contents.FromJson<IEnumerable<ArchiveModel>>();
        var document = documents.FirstOrDefault(x => x.Id == id);
        return document?.ToDomain();
    }
}

public interface IArchiveRepository : IRepository<ArchiveEntry>
{
    Task<ArchiveEntry?> GetEntry(int id);
    Task<IEnumerable<ArchiveEntry>> GetEntries();
}
