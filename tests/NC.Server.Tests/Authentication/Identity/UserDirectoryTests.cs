using MongoDB.Bson;
using NC.Server.Authentication.Identity;
using NC.Server.Tests.Authentication.Fakes;
using Xunit;

namespace NC.Server.Tests.Authentication.Identity;

public sealed class UserDirectoryTests
{
    [Fact]
    public async Task ReadAll_WhenOneProfileIsMalformed_ShouldReturnEveryUser()
    {
        var directory = new UserDirectory<TestProfile>(
            new FakeIdentityUsers(
                IdentityUser(1, "ana@example.test", new BsonDocument { ["club"] = "SC Ruse" }),
                IdentityUser(2, "boris@example.test", new BsonDocument { ["club"] = 5 }),
                IdentityUser(3, "petra@example.test", new BsonString("not a profile"))
            )
        );

        var users = await directory.ReadAll();

        Assert.Equal(3, users.Count);
        Assert.Equal("SC Ruse", users.Single(x => x.Id == 1).Profile.Club);
        Assert.Equal("boris@example.test", users.Single(x => x.Id == 2).Email);
        Assert.Null(users.Single(x => x.Id == 2).Profile.Club);
        Assert.Null(users.Single(x => x.Id == 3).Profile.Club);
    }

    [Fact]
    public async Task FindByEmail_WhenOneRowIsProved_ShouldReturnThatRow()
    {
        var directory = new UserDirectory<TestProfile>(
            new FakeIdentityUsers(
                IdentityUser(1, "ana@example.test", new BsonDocument()),
                IdentityUser(2, "ana@example.test", new BsonDocument(), proved: true)
            )
        );

        var user = await directory.FindByEmail("ana@example.test");

        Assert.Equal(2, user!.Id);
    }

    [Fact]
    public async Task FindByEmail_WhenTheProvedRowIsBehindLegacyRows_ShouldReturnIt()
    {
        var directory = new UserDirectory<TestProfile>(
            new FakeIdentityUsers(
                IdentityUser(1, "ana@example.test", new BsonDocument()),
                IdentityUser(2, "ana@example.test", new BsonDocument()),
                IdentityUser(3, "ana@example.test", new BsonDocument(), proved: true)
            )
        );

        var user = await directory.FindByEmail("ana@example.test");

        Assert.Equal(3, user!.Id);
    }

    [Fact]
    public async Task FindByEmail_WhenNoRowIsProved_ShouldReturnNull()
    {
        var directory = new UserDirectory<TestProfile>(
            new FakeIdentityUsers(
                IdentityUser(1, "ana@example.test", new BsonDocument()),
                IdentityUser(2, "ana@example.test", new BsonDocument())
            )
        );

        Assert.Null(await directory.FindByEmail("ana@example.test"));
    }

    [Fact]
    public async Task FindByEmail_WhenTheOnlyRowIsUnproved_ShouldReturnNull()
    {
        var directory = new UserDirectory<TestProfile>(
            new FakeIdentityUsers(IdentityUser(1, "ana@example.test", new BsonDocument()))
        );

        Assert.Null(await directory.FindByEmail("ana@example.test"));
    }

    [Fact]
    public async Task FindByEmail_WhenNoRowMatches_ShouldReturnNull()
    {
        var directory = new UserDirectory<TestProfile>(
            new FakeIdentityUsers(IdentityUser(1, "ana@example.test", new BsonDocument(), proved: true))
        );

        Assert.Null(await directory.FindByEmail("boris@example.test"));
    }

    static IdentityUserDocument IdentityUser(int id, string email, BsonValue profile, bool proved = false)
    {
        return new IdentityUserDocument
        {
            Id = id,
            Email = email,
            ProvedAtUtc = proved ? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) : null,
            Profile = profile,
        };
    }
}
