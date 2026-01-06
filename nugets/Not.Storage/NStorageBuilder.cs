using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using Not.Filesystem;
using Not.Storage.JsonFile.Stores;
using Not.Storage.JsonFile.Stores.Files;
using Not.Storage.Mongo;

namespace Not.Storage;

public class NStorageBuilder
{
    readonly IServiceCollection _services;

    public NStorageBuilder(IServiceCollection services, IConfiguration _)
    {
        _services = services;
    }

    public NStorageBuilder AddMongo(string connectionString)
    {
        var pack = new ConventionPack { new EnumRepresentationConvention(BsonType.String) };
        ConventionRegistry.Register("EnumStringConvention", pack, t => true);
        BsonSerializer.RegisterSerializer(typeof(DateTimeOffset), new DateTimeOffsetSerializer(BsonType.DateTime));

        _services.AddSingleton<IMongoContext, MongoContext>(x => new MongoContext(connectionString));
        return this;
    }

    public NStorageBuilder AddJsonFile()
    {
        var factory = FileContextHelper.CreateFileContextFactory(null, "stores");
        _services.AddKeyedSingleton<IFileContext, FileContext>(StoreConstants.DATA_KEY, factory);
        _services.AddSingleton(typeof(IStore<>), typeof(LockingJsonFileStore<>));
        return this;
    }
}
