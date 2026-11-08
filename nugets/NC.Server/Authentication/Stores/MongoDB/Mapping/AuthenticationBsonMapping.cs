using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using NC.Server.Authentication.Codes;
using NC.Server.Authentication.Email.Outbox;
using NC.Server.Authentication.Identity;
using NC.Server.Authentication.RateLimits;
using NC.Server.Authentication.Sessions;
using NC.Storage.Mongo;

namespace NC.Server.Authentication.Stores.MongoDB.Mapping;

/// <summary>
/// Maps the sign-in documents to MongoDB. The documents carry no database attributes, so this is the one place their
/// storage is decided: the identifier of each, and which dates are UTC. Null and default values are not written, by the
/// convention NC.Storage applies to every type.
/// </summary>
internal static class AuthenticationBsonMapping
{
    /// <summary>Registers the class maps once. Later calls do nothing.</summary>
    public static void Register()
    {
        // The class maps read the conventions that are registered when they are created, so these go first.
        MongoConventions.Register();

        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        Map<IdentityPasskeyDocument>(map =>
        {
            map.AutoMap();
            map.SetIgnoreExtraElements(true);
            map.MapMember(x => x.CreatedAtUtc).SetSerializer(Utc());
            map.MapMember(x => x.LastUsedAtUtc).SetSerializer(NullableUtc());
        });

        Map<IdentityUserDocument>(map =>
        {
            map.AutoMap();
            map.SetIgnoreExtraElements(true);
            map.MapIdMember(x => x.Id);
            map.MapMember(x => x.ProvedAtUtc).SetSerializer(NullableUtc());
            map.MapMember(x => x.LockoutEndUtc).SetSerializer(NullableUtc());
        });

        Map<SessionDocument>(map =>
        {
            map.AutoMap();
            map.SetIgnoreExtraElements(true);
            map.MapIdMember(x => x.TokenHash);
            map.MapMember(x => x.CreatedAtUtc).SetSerializer(Utc());
            map.MapMember(x => x.LastExtendedAtUtc).SetSerializer(Utc());
            map.MapMember(x => x.ExpiresAtUtc).SetSerializer(Utc());
        });

        Map<RegistrationDetailsDocument>(map =>
        {
            map.AutoMap();
            map.SetIgnoreExtraElements(true);
        });

        Map<CodeChallengeDocument>(map =>
        {
            map.AutoMap();
            map.SetIgnoreExtraElements(true);
            map.MapIdMember(x => x.Email);
            map.MapMember(x => x.LastSentAtUtc).SetSerializer(Utc());
            map.MapMember(x => x.ExpiresAtUtc).SetSerializer(Utc());
        });

        Map<RateCounterDocument>(map =>
        {
            map.AutoMap();
            map.SetIgnoreExtraElements(true);
            map.MapIdMember(x => x.Key);
            map.MapMember(x => x.ExpiresAtUtc).SetSerializer(Utc());
        });

        Map<OutboxMessageDocument>(map =>
        {
            map.AutoMap();
            map.SetIgnoreExtraElements(true);
            map.MapMember(x => x.SentAtUtc).SetSerializer(Utc());
        });
    }

    static int _registered;

    static void Map<T>(Action<BsonClassMap<T>> configure)
        where T : class
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(T)))
        {
            BsonClassMap.RegisterClassMap(configure);
        }
    }

    static IBsonSerializer Utc()
    {
        return new DateTimeSerializer(DateTimeKind.Utc);
    }

    static IBsonSerializer NullableUtc()
    {
        return new NullableSerializer<DateTime>(new DateTimeSerializer(DateTimeKind.Utc));
    }
}
