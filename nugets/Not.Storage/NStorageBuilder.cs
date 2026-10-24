using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using Not.Application;
using Not.Injection;
using Not.Storage.Mongo;

namespace Not.Storage;

public class NStorageBuilder
{
    readonly IServiceCollection _services;
    readonly NApplicationBuilder _nApplicationBuilder;
    static int _serializersRegistered;

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
            new EnumRepresentationConvention(BsonType.String),
        };
        ConventionRegistry.Register("DefaultConventions", pack, t => true);
        RegisterSerializers();

        _services.AddSingleton<IMongoContext, MongoContext>(x => new MongoContext(connectionString));
        _services.AddAsInterfaces(typeof(MongoRepository<>), ServiceLifetime.Transient, assembly);
        return this;
    }

    public NStorageBuilder AddRestApiStorage(Assembly _)
    {
        _nApplicationBuilder.AddHttp();
        return this;
    }

    /// <summary>
    /// The driver keeps one serializer per type for the whole process and refuses a second registration,
    /// so this runs once however many hosts are built. Every Guid is a standard-representation BSON UUID
    /// (ADR-0009, see <see cref="GuidSerialization"/>).
    /// </summary>
    static void RegisterSerializers()
    {
        if (Interlocked.Exchange(ref _serializersRegistered, 1) == 1)
        {
            return;
        }

        BsonSerializer.RegisterSerializer(typeof(DateTimeOffset), new DateTimeOffsetSerializer(BsonType.DateTime));
        GuidSerialization.Register();
    }
}
