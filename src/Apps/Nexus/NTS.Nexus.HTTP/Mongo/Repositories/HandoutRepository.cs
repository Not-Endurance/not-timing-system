using System.Diagnostics;
using System.Linq.Expressions;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Core;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class HandoutRepository : MongoRepository<HandoutModel>
{
    readonly ITelemetryService _telemetry;

    public HandoutRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.HANDOUTS_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override Expression<Func<HandoutModel, bool>> GetItemFilter(HandoutModel document)
    {
        return x => x.Id == document.Id && x.EventId == document.EventId;
    }

    protected override UpdateDefinition<HandoutModel> GetUpdateDefinition(HandoutModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(HandoutRepository), nameof(GetUpdateDefinition));

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
