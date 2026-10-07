using MongoDB.Bson;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Tests.Integration.Infrastructure;
using NTS.Tools.EnvironmentMarking;
using NTS.Tools.Staging;

namespace NTS.Tests.Integration;

/// <summary>
/// The command lines of <c>seed-staging</c> and <c>mark-environment</c> (#607): a dry run unless they are given
/// <c>--apply</c>, an option that is wrong or lacks its value is told with the usage, and what they print names neither the
/// connection string nor the email of anybody.
/// </summary>
public sealed class EnvironmentToolsTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset NOW = new(2031, 3, 14, 12, 0, 0, TimeSpan.Zero);

    readonly MongoFixture _mongo;

    public EnvironmentToolsTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task The_seed_without_apply_reports_and_changes_nothing()
    {
        var data = NewData();

        var (code, output, error) = await SeedAsync(data, "--environment", "Staging");

        Assert.Equal(0, code);
        Assert.Contains("Dry-run staging seed.", output);
        Assert.Contains($"Database: {data.Database.DatabaseNamespace.DatabaseName}", output);
        Assert.Equal("", error);
        Assert.Empty(await data.AllAsync());
    }

    [Fact]
    public async Task The_seed_with_apply_makes_the_Event_and_prints_neither_the_connection_string_nor_an_email()
    {
        var data = NewData();

        var (code, output, error) = await SeedAsync(
            data,
            "--apply",
            "--environment",
            "development",
            "--official",
            "steward@example.test:Steward",
            "--operator",
            "operator@example.test",
            "--event-name",
            "Spring Ride",
            "--days",
            "2",
            "--start",
            "06:30"
        );

        Assert.Equal(0, code);
        Assert.Contains("Applied.", output);
        Assert.Equal("Development", await EnvironmentMarker.ReadAsync(data.Database));
        Assert.Equal(4, await data.Collection("users").CountDocumentsAsync(new BsonDocument()));
        var setup = await data.Collection("configure_events").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        Assert.Equal("Spring Ride", setup["Name"].AsString);
        Assert.Equal(
            new DateTime(2031, 3, 14, 6, 30, 0, DateTimeKind.Utc),
            setup["Competitions"][0]["Start"].ToUniversalTime()
        );
        foreach (var text in new[] { output, error })
        {
            Assert.DoesNotContain(_mongo.ConnectionString, text);
            Assert.DoesNotContain("@example.test", text);
        }
    }

    [Fact]
    public async Task The_seed_is_refused_for_a_database_that_is_marked_Production_with_a_status_that_tells_so()
    {
        var data = NewData();
        await EnvironmentMarker.WriteAsync(data.Database, "Production", NOW);

        var (code, output, _) = await SeedAsync(data, "--apply", "--environment", "Staging");

        Assert.Equal(1, code);
        Assert.Contains("Refused:", output);
        Assert.Contains("Production", output);
        Assert.Equal(0, await data.Collection("users").CountDocumentsAsync(new BsonDocument()));
    }

    [Theory]
    [InlineData("--official", "steward@example.test")] // no role
    [InlineData("--official", "steward@example.test:Judge")] // a role that is none of ours
    [InlineData("--days", "many")]
    [InlineData("--start", "6")]
    [InlineData("--unknown", "x")]
    [InlineData("--operator", null)] // without its value
    public async Task The_seed_tells_an_option_that_is_wrong_or_lacks_its_value_and_shows_the_usage(
        string option,
        string? value
    )
    {
        var output = new StringWriter();
        var error = new StringWriter();
        string[] args =
            value == null
                ? ["--connection-string", _mongo.ConnectionString, option]
                : ["--connection-string", _mongo.ConnectionString, option, value];

        var code = await StagingSeedTool.Run(args, output, error, NOW);

        Assert.Equal(1, code);
        Assert.NotEqual("", error.ToString());
        Assert.Contains("Usage:", output.ToString());
    }

    [Fact]
    public async Task The_seed_without_a_connection_string_says_so_and_help_describes_the_options()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await StagingSeedTool.Run([], output, error, NOW);
        var help = new StringWriter();
        var helped = await StagingSeedTool.Run(["--help"], help, new StringWriter(), NOW);

        Assert.Equal(1, code);
        Assert.Contains("--connection-string is required.", error.ToString());
        Assert.Equal(0, helped);
        foreach (
            var option in new[]
            {
                "--tenant-root",
                "--main-operator",
                "--official",
                "--operator",
                "--environment",
                "--days",
                "--start",
                "--apply",
            }
        )
        {
            Assert.Contains(option, help.ToString());
        }
    }

    [Fact]
    public async Task Marking_without_apply_says_what_it_would_write_and_writes_nothing()
    {
        var data = NewData();

        var (code, output, _) = await MarkAsync(data, "--environment", "Production");

        Assert.Equal(0, code);
        Assert.Contains("Would mark the database as Production.", output);
        Assert.Null(await EnvironmentMarker.ReadAsync(data.Database));
    }

    [Fact]
    public async Task Marking_with_apply_writes_the_marker_and_doing_it_again_is_a_no_op()
    {
        var data = NewData();

        var (code, output, _) = await MarkAsync(data, "--apply", "--environment", "production");
        var (again, repeated, _) = await MarkAsync(data, "--apply", "--environment", "Production");

        Assert.Equal(0, code);
        Assert.Contains("The database is marked Production.", output);
        Assert.Equal("Production", await EnvironmentMarker.ReadAsync(data.Database));
        Assert.Equal(0, again);
        Assert.Contains("nothing to do", repeated);
    }

    [Fact]
    public async Task A_marker_is_not_changed_to_another_name_and_a_name_that_is_not_an_environment_is_refused()
    {
        var data = NewData();
        await EnvironmentMarker.WriteAsync(data.Database, "Production", NOW);

        var (conflict, _, conflictError) = await MarkAsync(data, "--apply", "--environment", "Staging");
        var (unknown, _, unknownError) = await MarkAsync(data, "--apply", "--environment", "Prod");

        Assert.Equal(1, conflict);
        Assert.Contains("Production", conflictError);
        Assert.Equal(1, unknown);
        Assert.Contains("not an environment", unknownError);
        Assert.Equal("Production", await EnvironmentMarker.ReadAsync(data.Database));
    }

    [Theory]
    [InlineData("--unknown")]
    [InlineData("--environment")]
    public async Task Marking_tells_an_option_that_is_wrong_or_lacks_its_value(string option)
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await MarkEnvironmentTool.Run(
            ["--connection-string", _mongo.ConnectionString, option],
            output,
            error,
            NOW
        );

        Assert.Equal(1, code);
        Assert.Contains("Unknown or incomplete option", error.ToString());
        Assert.Contains("Usage:", output.ToString());
    }

    [Fact]
    public async Task Marking_needs_the_connection_string_and_the_environment()
    {
        var withoutString = new StringWriter();
        var withoutEnvironment = new StringWriter();

        var first = await MarkEnvironmentTool.Run(["--environment", "Staging"], new StringWriter(), withoutString, NOW);
        var second = await MarkEnvironmentTool.Run(
            ["--connection-string", _mongo.ConnectionString],
            new StringWriter(),
            withoutEnvironment,
            NOW
        );

        Assert.Equal(1, first);
        Assert.Contains("--connection-string is required.", withoutString.ToString());
        Assert.Equal(1, second);
        Assert.Contains("--environment is required.", withoutEnvironment.ToString());
    }

    async Task<(int Code, string Output, string Error)> SeedAsync(LegacyTenantData data, params string[] more)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var args = new List<string>
        {
            "--connection-string",
            _mongo.ConnectionString,
            "--database",
            data.Database.DatabaseNamespace.DatabaseName,
            "--tenant-root",
            "root@example.test",
            "--main-operator",
            "main@example.test",
        };
        args.AddRange(more);
        var code = await StagingSeedTool.Run([.. args], output, error, NOW);
        return (code, output.ToString(), error.ToString());
    }

    async Task<(int Code, string Output, string Error)> MarkAsync(LegacyTenantData data, params string[] more)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var args = new List<string>
        {
            "--connection-string",
            _mongo.ConnectionString,
            "--database",
            data.Database.DatabaseNamespace.DatabaseName,
        };
        args.AddRange(more);
        var code = await MarkEnvironmentTool.Run([.. args], output, error, NOW);
        return (code, output.ToString(), error.ToString());
    }

    LegacyTenantData NewData()
    {
        return new LegacyTenantData(
            new MongoClient(_mongo.ConnectionString).GetDatabase("tools-" + Guid.NewGuid().ToString("N"))
        );
    }
}
