using Xunit;
using MongoDB.Bson;
using NC.Server.Authentication.Profiles;

namespace NC.Server.Tests.Authentication.Profiles;

public sealed class LegacyProfileMigrationTests
{
    [Fact]
    public void Migrate_WhenLegacyFieldsExist_ShouldMoveThemIntoProfile()
    {
        var row = new BsonDocument
        {
            ["Id"] = 7,
            ["Email"] = "ana@example.test",
            ["Club"] = "SC Ruse",
            ["FeiId"] = "10001234",
            ["CountryRegion"] = "BG",
        };

        var migrated = LegacyProfileMigration.Migrate(row);

        Assert.NotNull(migrated);
        var profile = migrated["profile"].AsBsonDocument;
        Assert.Equal("SC Ruse", profile["club"].AsString);
        Assert.Equal("10001234", profile["feiId"].AsString);
        Assert.Equal("BG", profile["countryIsoCode"].AsString);
        Assert.False(migrated.Contains("Club"));
        Assert.False(migrated.Contains("FeiId"));
        Assert.False(migrated.Contains("CountryRegion"));
    }

    [Fact]
    public void Migrate_WhenLegacyFieldsExist_ShouldKeepTheIdentityFields()
    {
        var row = new BsonDocument
        {
            ["Id"] = 7,
            ["Email"] = "ana@example.test",
            ["Club"] = "SC Ruse",
        };

        var migrated = LegacyProfileMigration.Migrate(row);

        Assert.NotNull(migrated);
        Assert.Equal(7, migrated["Id"].AsInt32);
        Assert.Equal("ana@example.test", migrated["Email"].AsString);
    }

    [Fact]
    public void Migrate_WhenNoLegacyFieldsExist_ShouldReturnNull()
    {
        var row = new BsonDocument
        {
            ["Id"] = 7,
            ["Email"] = "ana@example.test",
            ["profile"] = new BsonDocument { ["club"] = "SC Ruse" },
        };

        var migrated = LegacyProfileMigration.Migrate(row);

        Assert.Null(migrated);
    }

    [Fact]
    public void Migrate_WhenProfileAlreadyHasAValue_ShouldKeepIt()
    {
        var row = new BsonDocument
        {
            ["Id"] = 7,
            ["Club"] = "Old club",
            ["profile"] = new BsonDocument { ["club"] = "New club" },
        };

        var migrated = LegacyProfileMigration.Migrate(row);

        Assert.NotNull(migrated);
        Assert.Equal("New club", migrated["profile"].AsBsonDocument["club"].AsString);
    }
}
