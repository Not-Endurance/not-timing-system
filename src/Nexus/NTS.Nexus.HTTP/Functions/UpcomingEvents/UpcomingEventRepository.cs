using MongoDB.Driver;
using Not.Application.CRUD.Ports;
using NTS.Application.Mongo;
using NTS.Nexus.HTTP.Mongo;

namespace NTS.Nexus.HTTP.Functions.UpcomingEvents;

public class UpcomingEventRepository : MongoRepository<UpcomingEventDocument>, IUpcomingEventRepository
{
    public UpcomingEventRepository(IMongoContext context)
        : base(context, MongoConstants.NTS_DATABASE, "upcomingEvents") { }

    protected override UpdateDefinition<UpcomingEventDocument> GetUpdateDefinition(UpcomingEventDocument document)
    {
        return Builders<UpcomingEventDocument>
            .Update.Set(x => x.Place, document.Place)
            .Set(x => x.Country, document.Country)
            .Set(x => x.ShowFeiId, document.ShowFeiId)
            .Set(x => x.Competitions, document.Competitions)
            .Set(x => x.Officials, document.Officials)
            .Set(x => x.Loops, document.Loops)
            .Set(x => x.Combinations, document.Combinations);
    }
}

public interface IUpcomingEventRepository : IRepository<UpcomingEventDocument>
{
}
