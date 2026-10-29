using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Ui.Features.Account;
using NoTiming.Ui.Features.Sessions;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Tests.Integration.Harness;

namespace NTS.Tests.Integration;

/// <summary>
/// The history of what a person sent, as the host keeps it (#618): each group with the id it was sent under and each time with its
/// instant, which a correction of the time of an entry names; and the correction of an entry, which is kept in the group it is
/// of and nowhere else. What was kept before the ids were is still read, without them. Over the real Api in this process.
/// </summary>
public sealed class UserSessionHistoryTests : IClassFixture<SharedApiFixture>
{
    static readonly DateTimeOffset YESTERDAY = new(2026, 10, 11, 23, 59, 30, TimeSpan.FromHours(3));

    readonly SharedApiFixture _shared;

    public UserSessionHistoryTests(SharedApiFixture shared)
    {
        _shared = shared;
    }

    [Fact]
    public async Task A_group_that_is_sent_is_kept_with_its_id_and_the_instant_of_each_time_and_comes_back_in_another_tab()
    {
        var api = _shared.Api;
        using var client = api.CreateClient();
        var ana = await SignedInAsync(api, client);
        var eventId = Guid.NewGuid();
        using var first = Open(api, ana);
        await first.Sessions.SetEventId(eventId);
        var group = Sent(SnapshotType.Arrive, YESTERDAY, 7, 12);
        await first.Sessions.AppendSnapshot(group);
        using var second = Open(api, ana);
        await second.Sessions.SetEventId(eventId);

        var kept = Assert.Single((await second.Sessions.GetCurrent())!.SnapshotHistory);
        var back = kept.MapToDomain();

        Assert.Equal(group.Id, kept.GroupId);
        Assert.Equal(group.Id, back.Id);
        Assert.Equal([YESTERDAY, YESTERDAY], back.Entries.Select(x => x.Timestamp!.ToDateTimeOffset())); // the instant, and not the time of the day of today
        Assert.Equal(group.Entries.Select(group.IdOf), back.Entries.Select(back.IdOf));
        var stored = await StoredGroupAsync(ana, eventId);
        Assert.Equal(group.Id, stored["GroupId"].AsGuid);
        Assert.Equal(YESTERDAY, DateTimeOffset.Parse(stored["Entries"][0]["Time"].AsString));
    }

    [Fact]
    public async Task The_time_of_an_entry_is_corrected_in_the_group_it_is_of_and_in_no_other_entry()
    {
        var api = _shared.Api;
        using var client = api.CreateClient();
        var ana = await SignedInAsync(api, client);
        var eventId = Guid.NewGuid();
        using var tab = Open(api, ana);
        await tab.Sessions.SetEventId(eventId);
        var arrivals = Sent(SnapshotType.Arrive, YESTERDAY, 7, 12);
        var presentations = Sent(SnapshotType.Present, YESTERDAY.AddMinutes(10), 7);
        await tab.Sessions.AppendSnapshot(arrivals);
        await tab.Sessions.AppendSnapshot(presentations);
        var corrected = new Timestamp(YESTERDAY.AddMinutes(-4));

        await tab.Sessions.UpdateSnapshotInHistory(arrivals.Id, 7, corrected);

        using var another = Open(api, ana);
        await another.Sessions.SetEventId(eventId);
        var history = (await another.Sessions.GetCurrent())!.GetSnapshotHistory();
        Assert.Equal(
            [YESTERDAY.AddMinutes(-4), YESTERDAY],
            history[0].Entries.Select(x => x.Timestamp!.ToDateTimeOffset())
        ); // the entry of 7 in the group of arrivals, and 12 as it was
        Assert.Equal([YESTERDAY.AddMinutes(10)], history[1].Entries.Select(x => x.Timestamp!.ToDateTimeOffset())); // the presentation of 7 is another Snapshot
        Assert.Equal(corrected.ToString(), history[0].Entries.First().Timestamp!.ToString());
        var kept = (await another.Sessions.GetCurrent())!.SnapshotHistory[0].Entries[0];
        Assert.Equal(corrected.ToString(), kept.Timestamp); // the text of the time of the day is the corrected one too, for what reads that
    }

    [Fact]
    public async Task A_correction_of_a_group_the_history_does_not_have_changes_nothing_and_is_not_an_error()
    {
        var api = _shared.Api;
        using var client = api.CreateClient();
        var ana = await SignedInAsync(api, client);
        using var tab = Open(api, ana);
        await tab.Sessions.SetEventId(Guid.NewGuid());
        var group = Sent(SnapshotType.Arrive, YESTERDAY, 7);
        await tab.Sessions.AppendSnapshot(group);

        await tab.Sessions.UpdateSnapshotInHistory(Guid.NewGuid(), 7, new Timestamp(YESTERDAY.AddHours(1)));
        await tab.Sessions.UpdateSnapshotInHistory(group.Id, 99, new Timestamp(YESTERDAY.AddHours(1)));

        var kept = Assert.Single((await tab.Sessions.GetCurrent())!.GetSnapshotHistory());
        Assert.Equal(YESTERDAY, Assert.Single(kept.Entries).Timestamp!.ToDateTimeOffset());
    }

    [Fact]
    public async Task A_group_kept_before_the_ids_were_is_read_without_one_and_with_the_times_of_the_day_it_had()
    {
        var api = _shared.Api;
        using var client = api.CreateClient();
        var ana = await SignedInAsync(api, client);
        var eventId = Guid.NewGuid();
        await SeedLegacyRecordAsync(ana, eventId);
        using var tab = Open(api, ana);
        await tab.Sessions.SetEventId(eventId);

        var kept = Assert.Single((await tab.Sessions.GetCurrent())!.SnapshotHistory);

        Assert.Null(kept.GroupId);
        Assert.Null(Assert.Single(kept.Entries).Time);
        Assert.Equal("10:15:30", Assert.Single(kept.MapToDomain().Entries).Timestamp!.ToString());
        await tab.Sessions.UpdateSnapshotInHistory(Guid.NewGuid(), 3, new Timestamp(YESTERDAY)); // and a correction does not touch it
        Assert.Equal(
            "10:15:30",
            Assert
                .Single(Assert.Single((await tab.Sessions.GetCurrent())!.GetSnapshotHistory()).Entries)
                .Timestamp!.ToString()
        );
    }

    static SnapshotGroup Sent(SnapshotType type, DateTimeOffset time, params int[] numbers)
    {
        return new SnapshotGroup(
            numbers.Select(number => new Snapshot(
                number,
                $"Rider {number}",
                $"Rider {number} in English",
                new Timestamp(time)
            )),
            type
        );
    }

    static Tab Open(ApiFactory api, TenancySeed.Person person)
    {
        var json = JsonApiClients.Of(api, person, out _);
        var account = new AccountSession(json);
        return new Tab(account, new WitnessUserSessionService(account, json));
    }

    /// <summary>The first group of the history as the host stores it, the way the Functions API wrote it and the way this host does.</summary>
    async Task<BsonDocument> StoredGroupAsync(TenancySeed.Person person, Guid eventId)
    {
        var record = await RecordsOf(person, eventId).FirstAsync();
        return record["State"]["SnapshotHistory"][0].AsBsonDocument;
    }

    /// <summary>A record as the Functions API made it: no id of the group, and the time of a Snapshot as the text of its time of the day.</summary>
    async Task SeedLegacyRecordAsync(TenancySeed.Person person, Guid eventId)
    {
        await Records()
            .InsertOneAsync(
                new BsonDocument
                {
                    { "Id", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
                    { "EventId", new BsonBinaryData(eventId, GuidRepresentation.Standard) },
                    { "UserIdentifier", person.Id.ToString() },
                    { "TenantId", "nts" },
                    {
                        "State",
                        new BsonDocument
                        {
                            {
                                "SnapshotHistory",
                                new BsonArray
                                {
                                    new BsonDocument
                                    {
                                        {
                                            "Entries",
                                            new BsonArray
                                            {
                                                new BsonDocument
                                                {
                                                    { "Number", 3 },
                                                    { "Name", "Rider 3" },
                                                    { "Timestamp", "10:15:30" },
                                                },
                                            }
                                        },
                                        { "Type", "Arrive" },
                                    },
                                }
                            },
                            { "SnapshotSelections", new BsonArray() },
                        }
                    },
                }
            );
    }

    IFindFluent<BsonDocument, BsonDocument> RecordsOf(TenancySeed.Person person, Guid eventId)
    {
        return Records()
            .Find(
                new BsonDocument
                {
                    { "UserIdentifier", person.Id.ToString() },
                    { "EventId", new BsonBinaryData(eventId, GuidRepresentation.Standard) },
                }
            );
    }

    IMongoCollection<BsonDocument> Records()
    {
        return new MongoClient(_shared.MongoConnectionString)
            .GetDatabase(UserSeed.DATABASE)
            .GetCollection<BsonDocument>("event_user_sessions");
    }

    async Task<TenancySeed.Person> SignedInAsync(ApiFactory api, HttpClient client)
    {
        return await TenancySeed.SignedInAsync(api, client, _shared.MongoConnectionString, null);
    }

    sealed class Tab : IDisposable
    {
        public Tab(AccountSession account, WitnessUserSessionService sessions)
        {
            Account = account;
            Sessions = sessions;
        }

        public AccountSession Account { get; }
        public WitnessUserSessionService Sessions { get; }

        public void Dispose()
        {
            Account.Dispose();
        }
    }
}
