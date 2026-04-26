using MongoDB.Driver;
using Not.Exceptions;
using Not.Storage.Mongo;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class EnduranceEventMongoRepository : MongoRepository<EnduranceEventModel>, IEventResetRepository
{
    public EnduranceEventMongoRepository(IMongoContext context)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.ENDURANCE_EVENTS_COLLECTION) { }

    protected override UpdateDefinition<EnduranceEventModel> GetUpdateDefinition(EnduranceEventModel document)
    {
        return Builders<EnduranceEventModel>
            .Update.Set(x => x.Country, document.Country)
            .Set(x => x.Name, document.Name)
            .Set(x => x.Location, document.Location)
            .Set(x => x.FeiShowId, document.FeiShowId)
            .Set(x => x.FeiId, document.FeiId)
            .Set(x => x.FeiEventCode, document.FeiEventCode)
            .Set(x => x.StartDay, document.StartDay)
            .Set(x => x.EndDay, document.EndDay);
    }

    public override async Task Create(EnduranceEventModel item)
    {
        var existing = await Read(item.Id);
        if (existing != null)
        {
            throw GuardHelper.Exception($"Could not insert. Active endurance event with ID '{item.Id}' already exists");
        }

        await base.Create(item);
    }

    public Task DeleteAllForEvent(int eventId)
    {
        return GetCollection().DeleteManyAsync(x => x.Id == eventId);
    }
}
