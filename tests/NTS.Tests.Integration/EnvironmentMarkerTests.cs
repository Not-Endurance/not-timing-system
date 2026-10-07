using MongoDB.Bson;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// The environment marker (#607): one document in the database that says which environment it is, written once by the
/// commands that prepare a database and read by the ones that must never touch a production one. It is the only thing
/// that can tell a staging database from a production one, since both hold the same collections. A marker is not
/// changed to another name: a database that is production stays so until somebody removes the document by hand.
/// </summary>
public sealed class EnvironmentMarkerTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset NOW = new(2031, 3, 14, 12, 0, 0, TimeSpan.Zero);

    readonly MongoFixture _mongo;

    public EnvironmentMarkerTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_database_that_was_never_marked_has_no_marker()
    {
        var database = NewDatabase();

        Assert.Null(await EnvironmentMarker.ReadAsync(database));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Development")]
    public async Task A_marker_that_was_written_is_read_back_by_its_name(string name)
    {
        var database = NewDatabase();

        var written = await EnvironmentMarker.WriteAsync(database, name, NOW);

        Assert.True(written);
        Assert.Equal(name, await EnvironmentMarker.ReadAsync(database));
    }

    [Fact]
    public async Task The_marker_is_one_document_of_its_own_with_the_name_and_the_instant_it_was_written()
    {
        var database = NewDatabase();

        await EnvironmentMarker.WriteAsync(database, "Staging", NOW);

        var document = await database
            .GetCollection<BsonDocument>("environment")
            .Find(new BsonDocument("_id", "environment"))
            .SingleAsync();
        Assert.Equal("Staging", document["Name"].AsString);
        Assert.Equal(NOW.UtcDateTime, document["WrittenAt"].ToUniversalTime());
        Assert.Equal(
            1,
            await database.GetCollection<BsonDocument>("environment").CountDocumentsAsync(new BsonDocument())
        );
    }

    [Fact]
    public async Task Writing_the_name_the_marker_has_already_changes_nothing_and_says_so()
    {
        var database = NewDatabase();
        await EnvironmentMarker.WriteAsync(database, "Staging", NOW);

        var written = await EnvironmentMarker.WriteAsync(database, "Staging", NOW.AddDays(1));

        Assert.False(written);
        var document = await database
            .GetCollection<BsonDocument>("environment")
            .Find(new BsonDocument("_id", "environment"))
            .SingleAsync();
        Assert.Equal(NOW.UtcDateTime, document["WrittenAt"].ToUniversalTime()); // the first instant stays
    }

    [Theory]
    [InlineData("Production", "Staging")]
    [InlineData("Staging", "Production")]
    [InlineData("Development", "Staging")]
    public async Task A_marker_is_not_changed_to_another_name(string existing, string other)
    {
        var database = NewDatabase();
        await EnvironmentMarker.WriteAsync(database, existing, NOW);

        var refused = await Assert.ThrowsAsync<EnvironmentMarkedException>(
            () => EnvironmentMarker.WriteAsync(database, other, NOW)
        );

        Assert.Equal(existing, refused.Existing);
        Assert.Equal(existing, await EnvironmentMarker.ReadAsync(database));
    }

    [Theory]
    [InlineData("production", "Production")]
    [InlineData("STAGING", "Staging")]
    [InlineData(" Development ", "Development")]
    public void A_name_is_known_in_any_case_and_comes_back_as_it_is_written(string typed, string canonical)
    {
        Assert.Equal(canonical, EnvironmentMarker.Canonical(typed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Prod")]
    [InlineData("Test")]
    public void Anything_else_is_not_the_name_of_an_environment(string? typed)
    {
        Assert.Null(EnvironmentMarker.Canonical(typed));
    }

    [Fact]
    public async Task A_name_that_is_not_an_environment_is_refused_and_nothing_is_written()
    {
        var database = NewDatabase();

        await Assert.ThrowsAsync<ArgumentException>(() => EnvironmentMarker.WriteAsync(database, "Prod", NOW));

        Assert.Null(await EnvironmentMarker.ReadAsync(database));
    }

    [Fact]
    public async Task A_marker_whose_name_is_not_an_environment_is_not_taken_for_one_and_is_not_overwritten()
    {
        var database = NewDatabase();
        await database
            .GetCollection<BsonDocument>("environment")
            .InsertOneAsync(new BsonDocument { { "_id", "environment" }, { "Name", "Prod" } });

        var read = await EnvironmentMarker.ReadAsync(database);

        Assert.Equal("Prod", read); // what is there is told as it is, so that nobody takes the database for another one
        await Assert.ThrowsAsync<EnvironmentMarkedException>(
            () => EnvironmentMarker.WriteAsync(database, "Staging", NOW)
        );
    }

    [Theory]
    [InlineData("{ }")]
    [InlineData("{ Name: 5 }")]
    [InlineData("{ Name: null }")]
    public async Task A_marker_with_no_name_or_one_that_is_not_text_reads_as_no_marker(string document)
    {
        var database = NewDatabase();
        var marker = BsonDocument.Parse(document);
        marker["_id"] = "environment";
        await database.GetCollection<BsonDocument>("environment").InsertOneAsync(marker);

        Assert.Null(await EnvironmentMarker.ReadAsync(database));
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("production", true)]
    [InlineData(" Production ", true)]
    [InlineData("Staging", false)]
    [InlineData("Development", false)]
    [InlineData(null, false)]
    public void Production_is_told_in_any_case_and_only_by_its_name(string? name, bool isProduction)
    {
        Assert.Equal(isProduction, EnvironmentMarker.IsProduction(name));
    }

    [Theory]
    [InlineData("Staging", true)]
    [InlineData("development", true)]
    [InlineData(" Development ", true)]
    [InlineData("Production", false)]
    [InlineData("production", false)]
    [InlineData("Prod", false)]
    [InlineData("staging-eu", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_Staging_and_Development_say_that_a_database_is_not_production(string? name, bool isNonProduction)
    {
        Assert.Equal(isNonProduction, EnvironmentMarker.IsNonProduction(name));
    }

    [Fact]
    public async Task Two_writes_at_once_leave_one_marker()
    {
        var database = NewDatabase();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(_ => Task.Run(() => EnvironmentMarker.WriteAsync(database, "Staging", NOW)))
        );

        Assert.Equal(1, results.Count(x => x)); // one wrote it
        Assert.Equal("Staging", await EnvironmentMarker.ReadAsync(database));
        Assert.Equal(
            1,
            await database.GetCollection<BsonDocument>("environment").CountDocumentsAsync(new BsonDocument())
        );
    }

    IMongoDatabase NewDatabase()
    {
        return new MongoClient(_mongo.ConnectionString).GetDatabase("marker-" + Guid.NewGuid().ToString("N"));
    }
}
