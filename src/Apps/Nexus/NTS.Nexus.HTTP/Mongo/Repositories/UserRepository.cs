using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Setup;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public class UserRepository : MongoRepository<UserModel>
{
    public UserRepository(IMongoContext context)
        : base(context, MongoConstants.NTS_DATABASE, MongoConstants.USERS_COLLECTION) { }

    protected override UpdateDefinition<UserModel> GetUpdateDefinition(UserModel document)
    {
        return Builders<UserModel>
            .Update.Set(x => x.Email, document.Email)
            .Set(x => x.Name, document.Name)
            .Set(x => x.Roles, document.Roles)
            .Set(x => x.TenantId, document.TenantId);
    }
}
