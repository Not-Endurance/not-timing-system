using MongoDB.Bson;
using NC.Server.Authentication.Profiles;
using NC.Server.Tests.Authentication.Fakes;
using Xunit;

namespace NC.Server.Tests.Authentication.Profiles;

public sealed class ProfileDocumentTests
{
    [Fact]
    public void ToBson_WhenProfileIsSet_ShouldUseCamelCaseKeys()
    {
        var profile = new TestProfile
        {
            Club = "SC Ruse",
            FeiId = "10001234",
            CountryIsoCode = "BG",
        };

        var document = ProfileDocument.ToBson(profile);

        Assert.Equal("SC Ruse", document["club"].AsString);
        Assert.Equal("10001234", document["feiId"].AsString);
        Assert.Equal("BG", document["countryIsoCode"].AsString);
    }

    [Fact]
    public void FromBson_WhenFieldsAreStored_ShouldRestoreTheProfile()
    {
        var document = new BsonDocument
        {
            ["club"] = "SC Ruse",
            ["feiId"] = "10001234",
            ["countryIsoCode"] = "BG",
        };

        var profile = ProfileDocument.FromBson<TestProfile>(document);

        Assert.Equal("SC Ruse", profile.Club);
        Assert.Equal("10001234", profile.FeiId);
        Assert.Equal("BG", profile.CountryIsoCode);
    }

    [Fact]
    public void ToBson_WhenValuesHaveSpaces_ShouldTrimThem()
    {
        var profile = new TestProfile { Club = "  SC Ruse ", CountryIsoCode = " BG" };

        var document = ProfileDocument.ToBson(profile);

        Assert.Equal("SC Ruse", document["club"].AsString);
        Assert.Equal("BG", document["countryIsoCode"].AsString);
    }

    [Fact]
    public void FromBson_WhenDocumentIsMissing_ShouldReturnAnEmptyProfile()
    {
        var profile = ProfileDocument.FromBson<TestProfile>(null);

        Assert.NotNull(profile);
        Assert.Null(profile.Club);
    }
}
