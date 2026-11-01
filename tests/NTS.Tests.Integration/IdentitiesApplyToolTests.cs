using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Tests.Integration.Infrastructure;
using NTS.Tools.IdentityMigration;

namespace NTS.Tests.Integration;

/// <summary>
/// ADR-0009, #637: the command line of <c>migrate-identities</c>. The dry run says what an apply would finalise and verifies the
/// planned result, and what would stop an apply; <c>--apply</c> converts, prints each step and each check, and exits non-zero when it
/// is refused or a check fails.
/// </summary>
public sealed class IdentitiesApplyToolTests : IClassFixture<MongoFixture>, IDisposable
{
    readonly MongoFixture _mongo;
    readonly string _folder = Path.Combine(
        AppContext.BaseDirectory,
        "identities-apply-" + Guid.NewGuid().ToString("N")
    );

    public IdentitiesApplyToolTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    string ReportPath => Path.Combine(_folder, "report.md");
    string DecisionsPath => Path.Combine(_folder, "decisions.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task A_dry_run_says_what_an_apply_would_finalise_and_verifies_the_planned_result_and_changes_nothing()
    {
        var (name, database) = await SeededAsync();
        await Rankings(database).InsertOneAsync(LegacyWorldData.Ranking(304, 900, (70, 0), (71, 0)));
        await Rankings(database).InsertOneAsync(LegacyWorldData.Ranking(305, 900, (70, 1), (71, 0)));
        var before = await LegacyWorldData.SnapshotAsync(database);

        var run = await RunAsync(name);

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Error);
        Assert.Equal(before, await LegacyWorldData.SnapshotAsync(database));
        Assert.Contains("Rankings of Events that have ended with no placings, finalised by an apply: 1", run.Output);
        Assert.Contains("Rankings that hold only some of their ranks, left alone: 1", run.Output);
        Assert.Contains("PASS No integer id or reference remains", run.Output);
        Assert.Contains("PASS Every reference resolves", run.Output);
        Assert.Contains("PASS Names in Core are unchanged", run.Output);
        Assert.Contains("PASS Every Athlete and Horse id is a Setup row's", run.Output); // the rows to make are among the planned ones
        Assert.Contains("PASS The counts of Events, Participations, Rankings and Handouts are unchanged", run.Output);
        Assert.Contains("PASS Every Ranking of an Event that has ended has its placings", run.Output);
        Assert.Contains(
            "1 Rankings of Events that have ended have no placings, and are finalised by the apply.",
            run.Output
        );
        Assert.Contains("holds only some of its ranks, and is left alone.", run.Output);
        Assert.Contains("An apply would be refused: 0", run.Output);
        var report = await File.ReadAllTextAsync(ReportPath);
        Assert.Contains("## What an apply would do", report);
        Assert.Contains("Ranking 304 of Event 900", report);
        Assert.Contains("Ranking 305 of Event 900", report);
    }

    [Fact]
    public async Task A_dry_run_says_an_apply_would_be_refused_and_why()
    {
        var (name, database) = await SeededAsync();
        await database
            .GetCollection<BsonDocument>("event_handouts")
            .UpdateOneAsync(
                new BsonDocument("_id", 400),
                new BsonDocument("$set", new BsonDocument("ParticipationId", "seventy-two"))
            );

        var run = await RunAsync(name);

        Assert.Equal(0, run.Code);
        Assert.Contains("An apply would be refused: 1", run.Output);
        Assert.Contains("event_handouts 400 ParticipationId", run.Output);
    }

    [Fact]
    public async Task Apply_converts_the_database_prints_each_step_and_each_check_and_exits_with_0()
    {
        var (name, database) = await SeededAsync();

        var run = await RunAsync(name, "--apply");

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Error);
        Assert.Contains("Applying the identity migration", run.Output);
        Assert.Contains("event_participations", run.Output);
        Assert.Contains("Setup rows made: 10; merged away: 1. Rankings finalised: 0.", run.Output); // three are placed already
        Assert.Contains("PASS No integer id or reference remains", run.Output);
        Assert.Contains("PASS Every Athlete and Horse id is a Setup row's", run.Output);
        Assert.DoesNotContain("FAIL", run.Output);
        Assert.Single(
            await database
                .GetCollection<BsonDocument>("event_informations")
                .Find(
                    new BsonDocument(
                        "_id",
                        new BsonBinaryData(LegacyGuids.Of("Event", null, 900), GuidRepresentation.Standard)
                    )
                )
                .ToListAsync()
        );
        Assert.True(File.Exists(ReportPath));
        Assert.True(File.Exists(DecisionsPath));
    }

    [Fact]
    public async Task Apply_that_is_refused_exits_with_1_says_why_and_writes_nothing_to_the_database()
    {
        var (name, database) = await SeededAsync();
        Directory.CreateDirectory(_folder);
        await File.WriteAllTextAsync(DecisionsPath, """{ "items": { "athlete: nobody <> athlete: noone": "same" } }""");
        var before = await LegacyWorldData.SnapshotAsync(database);

        var run = await RunAsync(name, "--apply");

        Assert.Equal(1, run.Code);
        Assert.Contains("Refused", run.Output);
        Assert.Contains("athlete: nobody <> athlete: noone", run.Output);
        Assert.Contains("Nothing was written to the database", run.Output);
        Assert.Equal(before, await LegacyWorldData.SnapshotAsync(database));
    }

    [Fact]
    public async Task Apply_exits_with_1_when_a_check_of_the_verification_fails()
    {
        var (name, database) = await SeededAsync();
        await database
            .GetCollection<BsonDocument>("event_handouts")
            .UpdateOneAsync(
                new BsonDocument("_id", 400),
                new BsonDocument("$set", new BsonDocument("ParticipationId", 9999))
            );

        var run = await RunAsync(name, "--apply");

        Assert.Equal(1, run.Code);
        Assert.Contains("FAIL Every reference resolves", run.Output);
        Assert.Contains("PASS No integer id or reference remains", run.Output);
    }

    [Fact]
    public async Task Apply_a_second_time_changes_nothing_and_passes()
    {
        var (name, database) = await SeededAsync();
        await RunAsync(name, "--apply");
        var once = await LegacyWorldData.SnapshotAsync(database);

        var again = await RunAsync(name, "--apply");

        Assert.Equal(0, again.Code);
        Assert.Equal(once, await LegacyWorldData.SnapshotAsync(database));
    }

    [Fact]
    public async Task A_dry_run_of_a_database_with_a_run_that_stopped_says_so_and_a_finished_run_does_not()
    {
        var (name, database) = await SeededAsync();
        var steps = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                IdentitiesMigration.Apply(
                    database,
                    IdentityDecisions.None,
                    new ApplyOptions
                    {
                        Now = LegacyWorldData.NOW,
                        AfterStep = _ =>
                            ++steps == 4 ? throw new InvalidOperationException("stopped") : Task.CompletedTask,
                    }
                )
        );

        var stopped = await RunAsync(name);
        var finished = await RunAsync(name, "--apply");
        var after = await RunAsync(name);

        Assert.Equal(0, stopped.Code);
        Assert.Contains("A run of --apply that stopped is kept in this database", stopped.Output);
        Assert.Equal(0, finished.Code);
        Assert.Contains("Finished a run that had stopped.", finished.Output);
        Assert.DoesNotContain("A run of --apply that stopped", after.Output);
    }

    [Fact]
    public async Task What_the_database_cannot_do_stops_the_run_with_a_message_and_not_with_a_trace()
    {
        var run = await RunAsync("bad/name", "--apply"); // a name the server refuses

        Assert.Equal(1, run.Code);
        Assert.Contains("The run stopped", run.Error);
        Assert.Contains("Run it again to finish", run.Error);
    }

    [Fact]
    public async Task A_database_that_cannot_be_reached_stops_the_run_with_a_message_and_not_with_a_trace()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await IdentitiesMigrationTool.Run(
            [
                "--connection-string",
                "mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=300", // nothing listens there: the driver gives up with a timeout
                "--report",
                ReportPath,
                "--decisions",
                DecisionsPath,
                "--apply",
            ],
            output,
            error,
            LegacyWorldData.NOW
        );

        Assert.Equal(1, code);
        Assert.Contains("The run stopped", error.ToString());
        Assert.Contains("Run it again to finish", error.ToString());
    }

    [Fact]
    public async Task A_name_of_a_database_that_cannot_be_one_is_told_and_not_traced()
    {
        var run = await RunAsync("bad.name");

        Assert.Equal(1, run.Code);
        Assert.Contains("--database", run.Error);
    }

    [Theory]
    [InlineData("mongodb://user:SECRET-WORD@host:not-a-port")] // a configuration the driver cannot read
    [InlineData("mongodb://user:SECRET-WORD@host/?maxPoolSize=-1")] // a value out of range
    [InlineData("mongodb://user@host/?authMechanism=SCRAM-SHA-1&appName=SECRET-WORD")] // an argument it refuses: no password
    [InlineData("mongodb://user:SECRET-WORD@host/?authMechanism=FOO")] // a mechanism it does not support
    public async Task A_connection_string_that_is_not_one_is_told_without_being_printed(string connectionString)
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await IdentitiesMigrationTool.Run(
            ["--connection-string", connectionString, "--apply"],
            output,
            error,
            LegacyWorldData.NOW
        );

        Assert.Equal(1, code);
        Assert.Contains("--connection-string", error.ToString());
        Assert.DoesNotContain("SECRET-WORD", output + error.ToString());
    }

    [Fact]
    public async Task The_help_tells_the_apply_option()
    {
        var run = await RunAsync(null, "--help");

        Assert.Equal(0, run.Code);
        Assert.Contains("--apply", run.Output);
    }

    static IMongoCollection<BsonDocument> Rankings(IMongoDatabase database)
    {
        return database.GetCollection<BsonDocument>("event_rankings");
    }

    async Task<(string Name, IMongoDatabase Database)> SeededAsync()
    {
        var name = "identities-apply-" + Guid.NewGuid().ToString("N");
        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(name);
        await LegacyWorldData.SeedAsync(database);
        return (name, database);
    }

    async Task<(int Code, string Output, string Error)> RunAsync(string? database, params string[] more)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        string[] named =
            database == null ? [] : ["--connection-string", _mongo.ConnectionString, "--database", database];
        var code = await IdentitiesMigrationTool.Run(
            [.. named, "--report", ReportPath, "--decisions", DecisionsPath, .. more],
            output,
            error,
            LegacyWorldData.NOW
        );
        return (code, output.ToString(), error.ToString());
    }
}
