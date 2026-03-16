using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Core;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class EnduranceEventRepository : MongoRepository<EnduranceEventModel>
{
    public EnduranceEventRepository(IMongoContext context)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.ENDURANCE_EVENTS_COLLECTION) { }

    protected override UpdateDefinition<EnduranceEventModel> GetUpdateDefinition(EnduranceEventModel document)
    {
        return Builders<EnduranceEventModel>
            .Update.Set(x => x.Country, document.Country)
            .Set(x => x.City, document.City)
            .Set(x => x.Location, document.Location)
            .Set(x => x.FeiShowId, document.FeiShowId)
            .Set(x => x.FeiId, document.FeiId)
            .Set(x => x.FeiEventCode, document.FeiEventCode)
            .Set(x => x.StartDay, document.StartDay)
            .Set(x => x.EndDay, document.EndDay);
    }
}
