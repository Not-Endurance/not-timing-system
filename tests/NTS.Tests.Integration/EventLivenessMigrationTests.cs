using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Api.Features.Tenancy;
using NTS.Tests.Integration.Infrastructure;
using NTS.Tools.EventLiveness;

namespace NTS.Tests.Integration;

/// <summary>
/// ADR-0007, <c>migrate-event-liveness</c>: an Event is Live until the end of its last day, and the stored flag that said
/// so is removed. The command lists the Events that were inactive and whose last day is still ahead, because under the
/// rule they are Live again, and refuses to apply while there are any. It runs here over Event documents in the shape the
/// code before #628 stored them (a flag that is true, false or not there, and the days as dates), on a MongoDB in a
/// container, with the clock of the command given by the test.
/// </summary>
public sealed class EventLivenessMigrationTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset NOW = new(2031, 3, 14, 12, 0, 0, TimeSpan.Zero); // not a day the suite will be run on

    readonly MongoFixture _mongo;

    public EventLivenessMigrationTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public void The_command_reads_the_collection_the_Api_keeps_the_Events_in()
    {
        Assert.Equal(TenantOwned.EVENT_INFORMATIONS, EventLivenessMigration.EVENTS);
    }

    [Fact]
    public async Task A_dry_run_lists_exactly_the_Events_that_are_inactive_and_have_a_last_day_ahead_and_writes_nothing()
    {
        var database = NewDatabase();
        await StoreAsync(database, "Live", NOW.AddDays(2), isActive: true);
        await StoreAsync(database, "Expired", NOW.AddDays(-3), isActive: false);
        await StoreAsync(database, "Over but not yet expired", NOW.AddHours(-1), isActive: true);
        var early = await StoreAsync(database, "Deactivated early", NOW.AddDays(5), isActive: false);
        var noFlag = await StoreAsync(database, "No flag", NOW.AddDays(1), isActive: null);
        await StoreAsync(database, "Deleted", NOW.AddDays(4), isActive: false, deleted: true);
        var before = await AllAsync(database);

        var report = await EventLivenessMigration.Run(database, apply: false, NOW);

        Assert.Equal(before, await AllAsync(database));
        Assert.Equal(new[] { early, noFlag }.Order(), GuidsOf(report.Revived.Select(x => x.Id)));
        Assert.Equal(6, report.Seen);
        Assert.Equal(3, report.Live); // the live one and the two that are listed
        Assert.Equal(2, report.Historic); // the one that expired, and the one that is over though its flag is still on
        Assert.Equal(1, report.Deleted);
        Assert.Equal(5, report.WithFlag);
        Assert.Empty(report.Unreadable);
        Assert.False(report.Applied);
        Assert.False(report.Refused);
    }

    [Fact]
    public async Task The_list_says_what_the_flag_was_and_when_the_last_day_ends()
    {
        var database = NewDatabase();
        await StoreAsync(database, "Deactivated early", NOW.AddDays(5), isActive: false);
        await StoreAsync(database, "No flag", NOW.AddDays(1), isActive: null);
        await StoreAsync(database, "Live", NOW.AddDays(2), isActive: true);

        var report = await EventLivenessMigration.Run(database, apply: false, NOW);

        var output = new StringWriter();
        report.WriteTo(output);
        var text = output.ToString();
        Assert.Contains("Deactivated early", text);
        Assert.Contains("IsActive false", text);
        Assert.Contains("IsActive not stored", text);
        Assert.Contains("2031-03-19", text);
    }

    [Fact]
    public async Task The_instant_the_last_day_ends_is_Historic_and_a_millisecond_before_it_is_Live()
    {
        var database = NewDatabase();
        await StoreAsync(database, "Ends now", NOW, isActive: false);
        var justBefore = await StoreAsync(database, "Ends in a moment", NOW.AddMilliseconds(1), isActive: false);

        var report = await EventLivenessMigration.Run(database, apply: false, NOW);

        Assert.Equal([justBefore], GuidsOf(report.Revived.Select(x => x.Id)));
        Assert.Equal(1, report.Live);
        Assert.Equal(1, report.Historic);
    }

    [Fact]
    public async Task Apply_refuses_while_that_list_is_not_empty_and_writes_nothing()
    {
        var database = NewDatabase();
        await StoreAsync(database, "Live", NOW.AddDays(2), isActive: true);
        await StoreAsync(database, "Deactivated early", NOW.AddDays(5), isActive: false);
        var before = await AllAsync(database);

        var report = await EventLivenessMigration.Run(database, apply: true, NOW);

        Assert.True(report.Refused);
        Assert.False(report.Applied);
        Assert.Single(report.Revived);
        Assert.Equal(before, await AllAsync(database));
    }

    [Fact]
    public async Task Apply_removes_the_flag_from_every_document_once_nobody_is_listed_and_touches_nothing_else()
    {
        var database = NewDatabase();
        var live = await StoreAsync(database, "Live", NOW.AddDays(2), isActive: true);
        var expired = await StoreAsync(database, "Expired", NOW.AddDays(-3), isActive: false);
        var over = await StoreAsync(database, "Over but not yet expired", NOW.AddHours(-1), isActive: true);
        var deleted = await StoreAsync(database, "Deleted", NOW.AddDays(4), isActive: false, deleted: true);
        var before = await AllAsync(database);

        var report = await EventLivenessMigration.Run(database, apply: true, NOW);

        Assert.True(report.Applied);
        Assert.False(report.Refused);
        Assert.Equal(4, report.Removed);
        var after = await AllAsync(database);
        Assert.Equal(4, after.Count);
        Assert.All(after.Values, document => Assert.False(document.Contains("IsActive")));
        foreach (var id in new[] { live, expired, over, deleted })
        {
            var expected = before[id].DeepClone().AsBsonDocument;
            expected.Remove("IsActive");
            Assert.Equal(expected, after[id]);
        }
    }

    [Fact]
    public async Task What_the_owner_resolves_is_not_listed_again_and_the_apply_goes_through()
    {
        var database = NewDatabase();
        var early = await StoreAsync(database, "Deactivated early", NOW.AddDays(5), isActive: false);
        var noFlag = await StoreAsync(database, "No flag", NOW.AddDays(1), isActive: null);
        Assert.True((await EventLivenessMigration.Run(database, apply: true, NOW)).Refused);

        // The owner corrects the last day of one and removes the other.
        var events = database.GetCollection<BsonDocument>(EventLivenessMigration.EVENTS);
        await events.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", EventSeed.Binary(early)),
            Builders<BsonDocument>.Update.Set("EndDay", new BsonDateTime(NOW.AddDays(-1).UtcDateTime))
        );
        await events.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", EventSeed.Binary(noFlag)));
        var report = await EventLivenessMigration.Run(database, apply: true, NOW);

        Assert.False(report.Refused);
        Assert.True(report.Applied);
        Assert.Equal(1, report.Removed);
    }

    [Fact]
    public async Task A_second_apply_changes_nothing_and_does_not_ask_about_the_Events_that_are_Live()
    {
        var database = NewDatabase();
        await StoreAsync(database, "Live", NOW.AddDays(2), isActive: true);
        await StoreAsync(database, "Expired", NOW.AddDays(-3), isActive: false);
        await EventLivenessMigration.Run(database, apply: true, NOW);
        var once = await AllAsync(database);

        var again = await EventLivenessMigration.Run(database, apply: true, NOW);

        Assert.False(again.Refused);
        Assert.True(again.Applied);
        Assert.Empty(again.Revived); // the Event that is Live has no flag now, which does not make it inactive
        Assert.Equal(0, again.WithFlag);
        Assert.Equal(0, again.Removed);
        Assert.Equal(once, await AllAsync(database));
    }

    [Fact]
    public async Task Events_that_never_had_the_flag_are_not_asked_about()
    {
        var database = NewDatabase();
        await StoreAsync(database, "Live", NOW.AddDays(2), isActive: null);
        await StoreAsync(database, "Historic", NOW.AddDays(-2), isActive: null);

        var report = await EventLivenessMigration.Run(database, apply: true, NOW);

        Assert.False(report.Refused);
        Assert.Empty(report.Revived);
        Assert.Equal(1, report.Live);
        Assert.Equal(1, report.Historic);
        Assert.Equal(0, report.Removed);
    }

    [Fact]
    public async Task A_last_day_that_is_not_a_date_cannot_be_judged_and_stops_an_apply()
    {
        var database = NewDatabase();
        var odd = await StoreAsync(database, "Odd", NOW.AddDays(2), isActive: true);
        await database
            .GetCollection<BsonDocument>(EventLivenessMigration.EVENTS)
            .UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", EventSeed.Binary(odd)),
                Builders<BsonDocument>.Update.Set("EndDay", "2026-10-08")
            );
        var before = await AllAsync(database);

        var report = await EventLivenessMigration.Run(database, apply: true, NOW);

        Assert.True(report.Refused);
        Assert.Equal([odd], GuidsOf(report.Unreadable.Select(x => x.Id)));
        Assert.Equal(before, await AllAsync(database));
    }

    [Fact]
    public async Task The_command_line_is_a_dry_run_until_it_is_told_to_apply_and_refuses_with_1_while_it_must()
    {
        var database = NewDatabase();
        await StoreAsync(database, "Live", NOW.AddDays(2), isActive: true);
        await StoreAsync(database, "Expired", NOW.AddDays(-3), isActive: false); // Live by the clock of the machine, which is not NOW
        var early = await StoreAsync(database, "Deactivated early", NOW.AddDays(5), isActive: false);
        var before = await AllAsync(database);

        var dryRun = await CommandAsync(database, []);
        var refused = await CommandAsync(database, ["--apply"]);

        Assert.Equal(0, dryRun.ExitCode);
        Assert.Contains("Dry-run", dryRun.Output);
        Assert.Contains("Deactivated early", dryRun.Output);
        Assert.DoesNotContain("Expired", dryRun.Output);
        Assert.Contains("Nothing was changed", dryRun.Output);
        Assert.Equal(1, refused.ExitCode);
        Assert.Contains("Refusing to apply", refused.Output);
        Assert.Equal(before, await AllAsync(database));

        await database
            .GetCollection<BsonDocument>(EventLivenessMigration.EVENTS)
            .DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", EventSeed.Binary(early)));
        var applied = await CommandAsync(database, ["--apply"]);
        var again = await CommandAsync(database, ["--apply"]);

        Assert.Equal(0, applied.ExitCode);
        Assert.Contains("Done.", applied.Output);
        Assert.Equal(0, again.ExitCode);
        Assert.All((await AllAsync(database)).Values, document => Assert.False(document.Contains("IsActive")));
    }

    [Fact]
    public async Task The_command_line_needs_a_connection_string_and_says_how_it_is_used_when_it_has_none()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await EventLivenessMigrationTool.Run(["--apply"], output, error, NOW);

        Assert.Equal(1, exitCode);
        Assert.Contains("--connection-string is required.", error.ToString());
        Assert.Contains("migrate-event-liveness", output.ToString());
    }

    [Fact]
    public async Task An_option_it_does_not_know_is_refused()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await EventLivenessMigrationTool.Run(
            ["--connection-string", _mongo.ConnectionString, "--nonsense"],
            output,
            error,
            NOW
        );

        Assert.Equal(1, exitCode);
        Assert.Contains("--nonsense", error.ToString());
    }

    IMongoDatabase NewDatabase()
    {
        return new MongoClient(_mongo.ConnectionString).GetDatabase("liveness-" + Guid.NewGuid().ToString("N"));
    }

    async Task<(int ExitCode, string Output, string Error)> CommandAsync(IMongoDatabase database, string[] extra)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        string[] args =
        [
            "--connection-string",
            _mongo.ConnectionString,
            "--database",
            database.DatabaseNamespace.DatabaseName,
            .. extra,
        ];
        var exitCode = await EventLivenessMigrationTool.Run(args, output, error, NOW);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>An Event document as the code before #628 kept it: the flag is true, false or not there.</summary>
    static async Task<Guid> StoreAsync(
        IMongoDatabase database,
        string name,
        DateTimeOffset end,
        bool? isActive,
        bool deleted = false
    )
    {
        var id = Guid.NewGuid();
        var document = new BsonDocument
        {
            { "_id", EventSeed.Binary(id) },
            { "TenantId", "country-bg" },
            { "Name", name },
            { "Location", "Sofia" },
            { "StartDay", new BsonDateTime(end.AddDays(-1).UtcDateTime) },
            { "EndDay", new BsonDateTime(end.UtcDateTime) },
        };
        if (isActive != null)
        {
            document["IsActive"] = isActive.Value;
        }

        if (deleted)
        {
            document["IsDeleted"] = true;
        }

        await database.GetCollection<BsonDocument>(EventLivenessMigration.EVENTS).InsertOneAsync(document);
        return id;
    }

    static IEnumerable<Guid> GuidsOf(IEnumerable<BsonValue> ids)
    {
        return ids.Select(x => x.AsBsonBinaryData.ToGuid(GuidRepresentation.Standard)).Order();
    }

    static async Task<Dictionary<Guid, BsonDocument>> AllAsync(IMongoDatabase database)
    {
        var all = await database
            .GetCollection<BsonDocument>(EventLivenessMigration.EVENTS)
            .Find(FilterDefinition<BsonDocument>.Empty)
            .ToListAsync();
        return all.ToDictionary(x => x["_id"].AsBsonBinaryData.ToGuid(GuidRepresentation.Standard));
    }
}
