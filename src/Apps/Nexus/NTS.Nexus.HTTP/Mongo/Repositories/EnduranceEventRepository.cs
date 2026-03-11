using System.Diagnostics;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Core;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class EnduranceEventRepository : MongoRepository<EnduranceEventModel>
{
    readonly ITelemetryService _telemetry;

    public EnduranceEventRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.ENDURANCE_EVENTS_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<EnduranceEventModel> GetUpdateDefinition(EnduranceEventModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(EnduranceEventRepository), nameof(GetUpdateDefinition));

        try
        {
            return Builders<EnduranceEventModel>
                .Update.Set(x => x.Country, document.Country)
                .Set(x => x.City, document.City)
                .Set(x => x.Location, document.Location)
                .Set(x => x.FeiShowId, document.FeiShowId)
                .Set(x => x.FeiId, document.FeiId)
                .Set(x => x.FeiEventCode, document.FeiEventCode)
                .Set(x => x.StartDay, document.StartDay)
                .Set(x => x.EndDay, document.EndDay);
        }
        catch (Exception ex)
        {
            Activity.Current.TagException(ex);
            throw;
        }
    }
}
