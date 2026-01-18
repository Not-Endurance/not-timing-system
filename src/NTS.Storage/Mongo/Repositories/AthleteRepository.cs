using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Setup;

namespace NTS.Storage.Mongo.Repositories;

public class AthleteRepository : MongoRepository<SetupAthleteModel>
{
    public AthleteRepository(IMongoContext context)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.ATHLETES_COLLECTION) { }

    protected override UpdateDefinition<SetupAthleteModel> GetUpdateDefinition(SetupAthleteModel document)
    {
        return Builders<SetupAthleteModel>
            .Update.Set(x => x.Names, document.Names)
            .Set(x => x.Club, document.Club)
            .Set(x => x.Country, document.Country)
            .Set(x => x.FeiId, document.FeiId);
    }
}
