using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using Not.Application;
using Not.Filesystem;
using Not.Storage.JsonFile.States;
using Not.Storage.JsonFile.Stores;
using Not.Storage.JsonFile.Stores.Files;
using Not.Storage.Mongo;

namespace Not.Storage;

public class NStorageBuilder
{
    readonly IServiceCollection _services;
    readonly NApplicationBuilder _nApplicationBuilder;

    public NStorageBuilder(IServiceCollection services, IConfiguration configuration)
    {
        _services = services;
        _nApplicationBuilder = new(services, configuration);
    }

    public NStorageBuilder AddMongoStorage(string connectionString)
    {
        var pack = new ConventionPack { new EnumRepresentationConvention(BsonType.String) };
        ConventionRegistry.Register("EnumStringConvention", pack, t => true);
        BsonSerializer.RegisterSerializer(typeof(DateTimeOffset), new DateTimeOffsetSerializer(BsonType.DateTime));

        _services.AddSingleton<IMongoContext, MongoContext>(x => new MongoContext(connectionString));
        return this;
    }

    public NStorageBuilder AddJsonFileStorage<T, TStore, TInterface>()
        where TInterface : class
        where TStore : LockingJsonFileStore<T>, TInterface
        where T : class, IState, new()
    {
        var factory = FileContextHelper.CreateFileContextFactory(null, "stores");
        _services.AddKeyedSingleton<IFileContext, FileContext>(StoreConstants.DATA_KEY, factory);
        _services.AddSingleton<TInterface, TStore>();
        _services.AddSingleton(x => (IStore<T>)x.GetRequiredService<TInterface>());
        return this;
    }

    public NStorageBuilder AddRestApiStorage()
    {
        _nApplicationBuilder.AddHttp();
        return this;
    }
}
