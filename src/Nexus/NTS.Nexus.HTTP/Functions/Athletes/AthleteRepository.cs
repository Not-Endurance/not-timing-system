using MongoDB.Driver;
using NTS.Application.Mongo;
using NTS.Nexus.HTTP.Mongo;

namespace NTS.Nexus.HTTP.Functions.Athletes;

public class AthleteRepository : MongoRepository<AthleteModel>
{
    public AthleteRepository(IMongoContext context)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.ATHLETES_COLLECTION) { }

    protected override UpdateDefinition<AthleteModel> GetUpdateDefinition(AthleteModel document)
    {
        return Builders<AthleteModel>
            .Update.Set(x => x.Names, document.Names)
            .Set(x => x.Club, document.Club)
            .Set(x => x.Country, document.Country)
            .Set(x => x.FeiId, document.FeiId);
    }
}
