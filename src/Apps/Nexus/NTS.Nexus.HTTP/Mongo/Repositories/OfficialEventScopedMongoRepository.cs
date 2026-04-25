using System.Diagnostics;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Core;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class OfficialEventScopedMongoRepository : EventScopedMongoRepository<OfficialModel>
{
    readonly ITelemetryService _telemetry;

    public OfficialEventScopedMongoRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.OFFICIALS_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<OfficialModel> GetUpdateDefinition(OfficialModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(OfficialEventScopedMongoRepository), nameof(GetUpdateDefinition));

        try
        {
            return Builders<OfficialModel>
                .Update.Set(x => x.EventId, document.EventId)
                .Set(x => x.Names, document.Names)
                .Set(x => x.Role, document.Role);
        }
        catch (Exception ex)
        {
            Activity.Current.TagException(ex);
            throw;
        }
    }
}
