using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Shared;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class SettingRepository : MongoRepository<SettingModel>
{
    readonly ITelemetryService _telemetry;

    public SettingRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, "nts", "settings")
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<SettingModel> GetUpdateDefinition(SettingModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(SettingRepository), nameof(GetUpdateDefinition));

        try
        {
            return Builders<SettingModel>
                .Update.Set(x => x.DetectionMode, document.DetectionMode)
                .Set(x => x.Country, document.Country);
        }
        catch (Exception ex)
        {
            ex.AttachToCurrentActivity();
            throw;
        }
    }
}
