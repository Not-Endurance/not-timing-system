using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using Not.Application;
using Not.Filesystem;
using Not.Injection;
using Not.Storage.JsonFile.Repositories;
using Not.Storage.JsonFile.States;
using Not.Storage.JsonFile.Stores;
using Not.Storage.JsonFile.Stores.Files;
using Not.Storage.Mongo;
using Not.Storage.REST;

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

    public NStorageBuilder AddMongoStorage(string connectionString, Assembly assembly)
    {
        var pack = new ConventionPack 
        { 
            new IgnoreExtraElementsConvention(true), // TODO: Remove after existing data set is normalized
            new EnumRepresentationConvention(BsonType.String) 
        };
        ConventionRegistry.Register("DefaultConventions", pack, t => true);
        BsonSerializer.RegisterSerializer(typeof(DateTimeOffset), new DateTimeOffsetSerializer(BsonType.DateTime));

        _services.AddSingleton<IMongoContext, MongoContext>(x => new MongoContext(connectionString));
        _services.AddAsInterfaces(typeof(MongoRepository<>), ServiceLifetime.Transient, assembly);
        return this;
    }

    public NStorageBuilder AddJsonFileStorage<T, TStore, TInterface>(Assembly assembly)
        where TInterface : class
        where TStore : LockingJsonFileStore<T>, TInterface
        where T : class, IState, new()
    {
        var factory = FileContextHelper.CreateFileContextFactory("stores");
        _services.AddKeyedSingleton<IFileContext, FileContext>(StoreConstants.DATA_KEY, factory);
        _services.AddSingleton<TInterface, TStore>();
        _services.AddSingleton(x => (IStore<T>)x.GetRequiredService<TInterface>());
        _services.AddAsInterfaces(typeof(ReadonlyRootRepository<,>), ServiceLifetime.Transient, assembly);
        _services.AddAsInterfaces(typeof(ReadonlySetRepository<,>), ServiceLifetime.Transient, assembly);
        return this;
    }

    public NStorageBuilder AddRestApiStorage(Assembly assembly)
    {
        _nApplicationBuilder.AddHttp();
        _services.AddAsInterfaces(typeof(RestApiRepository<>), ServiceLifetime.Transient, assembly);
        return this;
    }
}
