using System.Diagnostics;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class RankingEventScopedMongoRepository : EventScopedMongoRepository<RankingModel>
{
    readonly ITelemetryService _telemetry;

    public RankingEventScopedMongoRepository(IMongoContext context, ITelemetryService telemetry)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.RANKINGS_COLLECTION)
    {
        _telemetry = telemetry;
    }

    protected override UpdateDefinition<RankingModel> GetUpdateDefinition(RankingModel document)
    {
        using var activity = _telemetry.StartActivity(nameof(RankingEventScopedMongoRepository), nameof(GetUpdateDefinition));

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
