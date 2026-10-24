using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Not.Identity.Mongo;

internal static class MongoSerialization
{
    /// <summary>
    /// Every Guid, an <c>_id</c> included, is a standard-representation BSON UUID (ADR-0009). The driver keeps one
    /// serializer per type for the whole process, so this registers once and checks that what is in force is the
    /// standard one: nothing relies on the driver's default.
    /// </summary>
    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        if (
            BsonSerializer.LookupSerializer<Guid>()
            is not GuidSerializer { GuidRepresentation: GuidRepresentation.Standard }
        )
        {
            throw new InvalidOperationException(
                "The Guid serializer of the MongoDB driver is not the standard-representation one. Register it before the first Guid is serialized."
            );
        }
    }

    static int _registered;
}
