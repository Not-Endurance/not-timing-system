using MongoDB.Driver;
using Not.Application.CRUD.Ports;
using NTS.Application.Mongo;
using NTS.Nexus.HTTP.Mongo;
using NTS.Storage.Documents.Archive;

namespace NTS.Nexus.HTTP.Functions.Archive;

public class ArchiveRepository : MongoRepository<ArchiveDocument>, IArchiveRepository
{
    public ArchiveRepository(IMongoContext context)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.ARCHIVE_COLLECTION) { }

    protected override UpdateDefinition<ArchiveDocument> GetUpdateDefinition(ArchiveDocument document)
    {
        return Builders<ArchiveDocument>
            .Update.Set(x => x.Officials, document.Officials)
            .Set(x => x.Ranklists, document.Ranklists)
            .Set(x => x.EndDay, document.EndDay)
            .Set(x => x.StartDay, document.StartDay)
            .Set(x => x.City, document.City)
            .Set(x => x.Country, document.Country)
            .Set(x => x.Location, document.Location);
    }

    public async Task<IEnumerable<ArchiveDocument>> GetPerformances(int horseId)
    {
        return await GetCollection()
            .Aggregate()
            .Match(x => x.Ranklists.Any(y => y.Entries.Any(z => z.Participation.Combination.Horse.Id == horseId)))
            //.Project(x =>
            //    x.Ranklists.SelectMany(y => y.Entries).First(z => z.Participation.Combination.Horse.Id == horseId)
            //)
            .ToListAsync();
    }
}

public interface IArchiveRepository : IRepository<ArchiveDocument>
{
    Task<IEnumerable<ArchiveDocument>> GetPerformances(int horseId);
}
