using MongoDB.Driver;
using Not.Application.CRUD.Ports;
using Not.Storage.Mongo;
using NTS.Application.Core;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class ArchiveRepository : MongoRepository<ArchiveModel>, IArchiveMongoRepository
{
    public ArchiveRepository(IMongoContext context)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.ARCHIVE_COLLECTION) { }

    protected override UpdateDefinition<ArchiveModel> GetUpdateDefinition(ArchiveModel document)
    {
        return Builders<ArchiveModel>
            .Update.Set(x => x.Officials, document.Officials)
            .Set(x => x.Ranklists, document.Ranklists)
            .Set(x => x.EndDay, document.EndDay)
            .Set(x => x.StartDay, document.StartDay)
            .Set(x => x.City, document.City)
            .Set(x => x.Country, document.Country)
            .Set(x => x.Location, document.Location);
    }

    public Task<IEnumerable<ArchiveModel>> GetPerformances(int horseId) // TODO: fix
    {
        throw new NotImplementedException();
        //return await GetCollection()
        //    .Aggregate()
        //    .Match(x => x.Ranklists.Any(y => y.Entries.Any(z => z.Participation.Combination.Horse.Id == horseId)))
        //    //.Project(x =>
        //    //    x.Ranklists.SelectMany(y => y.Entries).First(z => z.Participation.Combination.Horse.Id == horseId)
        //    //)
        //    .ToListAsync();
    }
}

public interface IArchiveMongoRepository : IRepository<ArchiveModel>
{
    Task<IEnumerable<ArchiveModel>> GetPerformances(int horseId);
}
