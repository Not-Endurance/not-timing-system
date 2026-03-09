using System.Diagnostics;
using MongoDB.Driver;
using Not.Application.CRUD.Ports;
using Not.Storage.Mongo;
using NTS.Application.Setup;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class UpcomingEventMongoRepository : MongoRepository<UpcomingEventModel>, IRepository<UpcomingEventModel>
{
    readonly ITelemetryService _telemetry;

    public UpcomingEventMongoRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, "upcomingEvents")
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<UpcomingEventModel> GetUpdateDefinition(UpcomingEventModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(UpcomingEventMongoRepository), nameof(GetUpdateDefinition));

        try
        {
            return Builders<UpcomingEventModel> // TODO: use Reflection to build a full update definition by default
                .Update.Set(x => x.Place, document.Place)
                .Set(x => x.Name, document.Name)
                .Set(x => x.Country, document.Country)
                .Set(x => x.ShowFeiId, document.ShowFeiId)
                .Set(x => x.FeiId, document.FeiId)
                .Set(x => x.FeiEventCode, document.FeiEventCode)
                .Set(x => x.Competitions, document.Competitions)
                .Set(x => x.Officials, document.Officials)
                .Set(x => x.Loops, document.Loops)
                .Set(x => x.Combinations, document.Combinations);
        }
        catch (Exception ex)
        {
            Activity.Current.TagException(ex);
            throw;
        }
    }
}
