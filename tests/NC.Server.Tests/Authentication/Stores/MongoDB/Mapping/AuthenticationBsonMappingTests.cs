using MongoDB.Bson;
using NC.Server.Authentication.Sessions;
using NC.Server.Authentication.Stores.MongoDB.Mapping;
using Xunit;

namespace NC.Server.Tests.Authentication.Stores.MongoDB.Mapping;

public sealed class AuthenticationBsonMappingTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Register_WhenSessionIsSerialized_ShouldUseTokenHashAsId()
    {
        AuthenticationBsonMapping.Register();

        var document = SessionWithExpiry(Now.AddDays(30)).ToBsonDocument();

        Assert.Equal("hash", document["_id"].AsString);
    }

    [Fact]
    public void Register_WhenSessionIsSerialized_ShouldStoreExpiryAsUtcDate()
    {
        AuthenticationBsonMapping.Register();

        var document = SessionWithExpiry(Now.AddDays(30)).ToBsonDocument();

        Assert.Equal(Now.AddDays(30).UtcDateTime, document["ExpiresAtUtc"].AsBsonDateTime.ToUniversalTime());
    }

    static SessionDocument SessionWithExpiry(DateTimeOffset expiresAt)
    {
        return new SessionDocument
        {
            TokenHash = "hash",
            UserId = 7,
            SecurityStamp = "stamp",
            CreatedAtUtc = Now.UtcDateTime,
            LastExtendedAtUtc = Now.UtcDateTime,
            ExpiresAtUtc = expiresAt.UtcDateTime,
        };
    }
}
