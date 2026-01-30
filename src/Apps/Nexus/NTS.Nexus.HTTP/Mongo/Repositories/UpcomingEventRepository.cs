using MongoDB.Driver;
using Not.Application.CRUD.Ports;
using Not.Storage.Mongo;
using NTS.Application.Setup;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class UpcomingEventRepository : MongoRepository<UpcomingEventModel>, IRepository<UpcomingEventModel>
{
    public UpcomingEventRepository(IMongoContext context)
        : base(context, MongoConstants.NTS_DATABASE, "upcomingEvents") { }

    protected override UpdateDefinition<UpcomingEventModel> GetUpdateDefinition(UpcomingEventModel document)
    {
        return Builders<UpcomingEventModel> // TODO: use Reflection to build a full update definition by default
            .Update.Set(x => x.Place, document.Place)
            .Set(x => x.Name, document.Name)
            .Set(x => x.Country, document.Country)
            .Set(x => x.ShowFeiId, document.ShowFeiId)
            .Set(x => x.FeiId, document.FeiId)
            .Set(x => x.FeiEventCode, document.FeiEventCode)
            .Set(x => x.Competitions, document.Competitions)
            .Set(x => x.Officials, document.Officials)
            .Set(x => x.Loops, document.Loops)
            .Set(x => x.Combinations, document.Combinations);
    }
}
