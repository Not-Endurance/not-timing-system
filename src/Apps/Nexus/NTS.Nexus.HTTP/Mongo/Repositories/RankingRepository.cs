using System.Diagnostics;
using System.Linq.Expressions;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Core;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class RankingRepository : MongoRepository<RankingModel>
{
    readonly ITelemetryService _telemetry;

    public RankingRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.RANKINGS_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override Expression<Func<RankingModel, bool>> GetItemFilter(RankingModel document)
    {
        return x => x.Id == document.Id && x.EventId == document.EventId;
    }

    protected override UpdateDefinition<RankingModel> GetUpdateDefinition(RankingModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(RankingRepository), nameof(GetUpdateDefinition));

        try
        {
            return Builders<RankingModel>
                .Update.Set(x => x.Name, document.Name)
                .Set(x => x.Ruleset, document.Ruleset)
                .Set(x => x.Type, document.Type)
                .Set(x => x.Category, document.Category)
                .Set(x => x.CompetitionFeiId, document.CompetitionFeiId)
                .Set(x => x.FeiRule, document.FeiRule)
                .Set(x => x.FeiScheduleNumber, document.FeiScheduleNumber)
                .Set(x => x.EventId, document.EventId)
                .Set(x => x.Entries, document.Entries);
        }
        catch (Exception ex)
        {
            Activity.Current.TagException(ex);
            throw;
        }
    }
}
