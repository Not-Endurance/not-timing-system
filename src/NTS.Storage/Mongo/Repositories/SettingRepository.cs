using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Models;

namespace NTS.Storage.Mongo.Repositories;

public class SettingRepository : MongoRepository<SettingModel>
{
    public SettingRepository(IMongoContext context)
        : base(context, "nts", "settings") { }

    protected override UpdateDefinition<SettingModel> GetUpdateDefinition(SettingModel document)
    {
        return Builders<SettingModel>
            .Update.Set(x => x.DetectionMode, document.DetectionMode)
            .Set(x => x.Country, document.Country);
    }
}
