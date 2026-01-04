using MongoDB.Driver;
using NTS.Application.Models;
using NTS.Application.Mongo;
using NTS.Nexus.HTTP.Mongo;

namespace NTS.Nexus.HTTP.Functions.Horses;

public class HorseRepository : MongoRepository<SetupHorseModel>
{
    public HorseRepository(IMongoContext context)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.HORSES_COLLECTION) { }

    protected override UpdateDefinition<SetupHorseModel> GetUpdateDefinition(SetupHorseModel document)
    {
        return Builders<SetupHorseModel>.Update.Set(x => x.Name, document.Name).Set(x => x.FeiId, document.FeiId);
    }
}
