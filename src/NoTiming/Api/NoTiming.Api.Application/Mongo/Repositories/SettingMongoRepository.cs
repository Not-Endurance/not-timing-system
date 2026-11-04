using System.Diagnostics;
using MongoDB.Driver;
using NC.Storage.Mongo;
using NoTiming.Api.Application.Telemetry;
using NoTiming.Application.Contracts.Shared;
using NoTiming.Application.Contracts.Shared.Models;

namespace NoTiming.Api.Application.Mongo.Repositories;

public class SettingMongoRepository : MongoRepository<SettingModel>
{
    readonly ITelemetryService _telemetry;

    public SettingMongoRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, "nts", "settings")
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<SettingModel> GetUpdateDefinition(SettingModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(SettingMongoRepository), nameof(GetUpdateDefinition));

        try
        {
            return Builders<SettingModel>
                .Update.Set(x => x.DetectionMode, document.DetectionMode)
                .Set(x => x.Country, document.Country);
        }
        catch (Exception ex)
        {
            Activity.Current.TagException(ex);
            throw;
        }
    }
}
