using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace NC.Storage.Mongo;

/// <summary>
/// The one set of MongoDB conventions for every type the storage writes: null and default values are not written, enum
/// values are written as their names, extra fields are ignored, and a DateTimeOffset is written as a date.
/// </summary>
public static class MongoConventions
{
    /// <summary>Registers the conventions once. Later calls do nothing.</summary>
    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        var pack = new ConventionPack
        {
            new IgnoreExtraElementsConvention(true), // TODO: Remove after existing data set is normalized
            new EnumRepresentationConvention(BsonType.String),
            new IgnoreNullOrDefaultConvention(),
        };
        ConventionRegistry.Register("DefaultConventions", pack, t => true);
        BsonSerializer.RegisterSerializer(typeof(DateTimeOffset), new DateTimeOffsetSerializer(BsonType.DateTime));
    }

    static int _registered;

    sealed class IgnoreNullOrDefaultConvention : IMemberMapConvention
    {
        public string Name => "Ignore null or default values";

        public void Apply(BsonMemberMap memberMap)
        {
            if (CanBeNull(memberMap.MemberType))
            {
                memberMap.SetIgnoreIfNull(true);
                return;
            }

            memberMap.SetIgnoreIfDefault(true);
        }

        static bool CanBeNull(Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }
    }
}
