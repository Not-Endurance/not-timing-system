using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

namespace NTS.Application.Mongo;

public static class NtsMongoSerialization
{
    public static void Configure()
    {
        if (Interlocked.Exchange(ref _configured, 1) == 1)
        {
            return;
        }

        var pack = new ConventionPack { new IgnoreNullOrDefaultConvention() };

        ConventionRegistry.Register(
            "NTS ignore null and default values",
            pack,
            type => type.FullName?.StartsWith("NTS.", StringComparison.Ordinal) == true
        );
    }

    static int _configured;

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
