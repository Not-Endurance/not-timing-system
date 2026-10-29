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
    /// <summary>
    /// The driver keeps one serializer per type for the whole process and refuses a second registration,
    /// so this runs once however many hosts are built, and accepts one that another part of the process
    /// registered first as long as it is the one wanted. Every Guid, an <c>_id</c> included, is written as a
    /// standard-representation BSON UUID (ADR-0009) and every date as a BSON date: nothing relies on the
    /// driver's default.
    /// </summary>
    public static void RegisterSerializers()
    {
        if (Interlocked.Exchange(ref _serializersRegistered, 1) == 1)
        {
            return;
        }

        BsonSerializer.TryRegisterSerializer(typeof(DateTimeOffset), new DateTimeOffsetSerializer(BsonType.DateTime));
        BsonSerializer.TryRegisterSerializer(typeof(Guid), new GuidSerializer(GuidRepresentation.Standard));
        if (
            BsonSerializer.LookupSerializer<Guid>()
                is not GuidSerializer { GuidRepresentation: GuidRepresentation.Standard }
            || BsonSerializer.LookupSerializer<DateTimeOffset>()
                is not DateTimeOffsetSerializer { Representation: BsonType.DateTime }
        )
        {
            throw new InvalidOperationException(
                "The Guid or the date serializer of the MongoDB driver is not the one the application needs. Register them before the first Guid or date is serialized."
            );
        }
    }

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
}
