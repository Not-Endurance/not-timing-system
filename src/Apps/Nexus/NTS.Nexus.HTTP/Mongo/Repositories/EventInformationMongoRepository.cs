using MongoDB.Driver;
using Not.Exceptions;
using Not.Storage.Mongo;
using NTS.Contracts.Core.Models;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class EventInformationMongoRepository : MongoRepository<EventInformationModel>
{
    public EventInformationMongoRepository(IMongoContext context)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.EVENT_INFORMATION_COLLECTION) { }

    protected override UpdateDefinition<EventInformationModel> GetUpdateDefinition(EventInformationModel document)
    {
        return Builders<EventInformationModel>
            .Update.Set(x => x.Country, document.Country)
            .Set(x => x.Name, document.Name)
            .Set(x => x.Location, document.Location)
            .Set(x => x.FeiShowId, document.FeiShowId)
            .Set(x => x.StartDay, document.StartDay)
            .Set(x => x.EndDay, document.EndDay);
    }

    public override async Task Create(EventInformationModel item)
    {
        var existing = await Read(item.Id);
        if (existing != null)
        {
            throw GuardHelper.Exception($"Could not insert. Event information with ID '{item.Id}' already exists");
        }

        await base.Create(item);
    }
}
