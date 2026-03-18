using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Core;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class EnduranceEventMongoRepository : SoftDeleteMongoRepository<EnduranceEventModel>, IEventResetRepository
{
    public EnduranceEventMongoRepository(IMongoContext context)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.ENDURANCE_EVENTS_COLLECTION) { }

    protected override void PrepareForCreate(EnduranceEventModel item)
    {
        if (string.IsNullOrWhiteSpace(item.MongoId))
        {
            item.MongoId = Guid.NewGuid().ToString("N");
        }

        base.PrepareForCreate(item);
    }

    protected override UpdateDefinition<EnduranceEventModel> GetUpdateDefinition(EnduranceEventModel document)
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

    public async Task<int?> GetMaxDeletedVersion(int eventId)
    {
        var deleted = await GetCollection()
            .Find(x => x.Id == eventId && x.IsDeleted)
            .SortByDescending(x => x.DeletedVersion)
            .FirstOrDefaultAsync();

        return deleted?.DeletedVersion;
    }

    public async Task SoftDelete(int eventId, int deletedVersion)
    {
        var filter = Builders<EnduranceEventModel>.Filter.Where(x => x.Id == eventId && x.IsDeleted != true);
        await GetCollection().UpdateManyAsync(filter, SoftDeleteUpdateDefinition(deletedVersion));
    }
}
