using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;

namespace NTS.Nexus.HTTP.Mongo;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMongo(this IServiceCollection services, string? connectionString)
    {
        var pack = new ConventionPack { new EnumRepresentationConvention(BsonType.String) };
        ConventionRegistry.Register("EnumStringConvention", pack, t => true);

        services.AddSingleton<IMongoContext, MongoContext>(x => new MongoContext(connectionString));
        return services;
    }
}
