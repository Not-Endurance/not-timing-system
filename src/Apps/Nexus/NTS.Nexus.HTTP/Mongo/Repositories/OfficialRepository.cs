using System.Diagnostics;
using System.Linq.Expressions;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Core;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class OfficialRepository : MongoRepository<OfficialModel>
{
    readonly ITelemetryService _telemetry;

    public OfficialRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.OFFICIALS_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override Expression<Func<OfficialModel, bool>> GetItemFilter(OfficialModel document)
    {
        return x => x.Id == document.Id && x.EventId == document.EventId;
    }

    protected override UpdateDefinition<OfficialModel> GetUpdateDefinition(OfficialModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(OfficialRepository), nameof(GetUpdateDefinition));

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
