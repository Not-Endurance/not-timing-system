using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using Not.Storage;
using NTS.Application.Mongo;

namespace NoTiming.Api.Features.Reference;

/// <summary>
/// How the documents of the application's models are mapped to and from MongoDB in this host, which is how the Functions
/// host maps them (#603): every Guid and every date as the driver is told to write them, the members that are null or
/// default not written, and a stored member the model does not know ignored. It is what lets the models of the reference
/// data be read and written as typed documents in one process with the account code, which keeps its own documents by
/// hand and is not touched by it: the conventions here apply to the types of the application and to no other. It runs
/// once, however many hosts a process builds.
/// </summary>
internal static class ApiMongo
{
    public static void Configure()
    {
        if (Interlocked.Exchange(ref _configured, 1) == 1)
        {
            return;
        }

        NStorageBuilder.RegisterSerializers();
        NtsMongoSerialization.Configure();
        ConventionRegistry.Register(
            "NTS models of the Api",
            new ConventionPack
            {
                new IgnoreExtraElementsConvention(true),
                new EnumRepresentationConvention(BsonType.String),
            },
            type => type.FullName?.StartsWith("NTS.", StringComparison.Ordinal) == true
        );
    }

    static int _configured;
}
