using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using Not.Storage;
using NTS.Application.Mongo;
using NTS.Contracts.Setup.Models;

namespace NTS.Tools.Shared;

/// <summary>
/// How the models of the application are mapped to and from documents: the way the Api maps them, so that what a command seeds or
/// finalises is read as it is. It runs once, however many commands a process makes.
/// </summary>
internal static class ModelMapping
{
    static readonly object CONVENTIONS = new();

    public static void Configure()
    {
        NStorageBuilder.RegisterSerializers();
        NtsMongoSerialization.Configure();
        lock (CONVENTIONS)
        {
            if (
                !ConventionRegistry
                    .Lookup(typeof(ConfigureEventModel))
                    .Conventions.Any(x => x is EnumRepresentationConvention)
            )
            {
                ConventionRegistry.Register(
                    "NTS models of the tools",
                    new ConventionPack
                    {
                        new IgnoreExtraElementsConvention(true),
                        new EnumRepresentationConvention(BsonType.String),
                    },
                    type => type.FullName?.StartsWith("NTS.", StringComparison.Ordinal) == true
                );
            }
        }
    }
}
