using MongoDB.Driver;
using NTS.Application.Models;
using NTS.Nexus.HTTP.Mongo;

namespace NTS.Nexus.HTTP.Functions.Settings;

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
