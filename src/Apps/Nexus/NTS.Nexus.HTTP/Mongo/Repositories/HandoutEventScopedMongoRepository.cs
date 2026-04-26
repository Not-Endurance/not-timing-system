using System.Diagnostics;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class HandoutEventScopedMongoRepository : EventScopedMongoRepository<HandoutModel>
{
    readonly ITelemetryService _telemetry;

    public HandoutEventScopedMongoRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.HANDOUTS_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<HandoutModel> GetUpdateDefinition(HandoutModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(HandoutEventScopedMongoRepository), nameof(GetUpdateDefinition));

        try
        {
            return Builders<HandoutModel>
                .Update.Set(x => x.EventId, document.EventId)
                .Set(x => x.Participation, document.Participation);
        }
        catch (Exception ex)
        {
            Activity.Current.TagException(ex);
            throw;
        }
    }
}
