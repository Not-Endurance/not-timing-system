using System.Net;
using Not.Application.HTTP;
using NoTiming.Ui.Features.Account;
using NoTiming.Ui.Features.Sessions;
using NTS.Contracts.Watcher.Models;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// What a person keeps per Event at the Ui (#645, #602): the Snapshots they have selected and the groups they have sent,
/// kept by the host under their account and read back in any tab or on any device. A visitor keeps nothing and the host is
/// not asked to keep it, nothing is kept before an Event is named, the state of one Event is not the state of another, and
/// the state of another person is never reached. Over the real Api in this process.
/// </summary>
public sealed class UserSessionStateTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public UserSessionStateTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_visitor_keeps_nothing_and_the_host_is_not_asked_to_keep_it()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var tab = Open(api, null, out var asked);
        await tab.Sessions.SetEventId(Guid.NewGuid());

        await tab.Sessions.ReplaceSnapshotSelections([Selected(7)]);
        await tab.Sessions.AppendSnapshot(Sent(SnapshotType.Present, 7));
        await tab.Sessions.DeleteCurrent();

        Assert.Null(await tab.Sessions.GetCurrent());
        Assert.DoesNotContain(asked.Asked, request => request.Contains("user-sessions"));
    }

    [Fact]
    public async Task A_person_who_has_kept_nothing_for_the_Event_has_no_state_and_naming_the_Event_does_not_make_one()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var ana = await SignedInAsync(api, client);
        using var tab = Open(api, ana, out var asked);

        await tab.Sessions.SetEventId(Guid.NewGuid());

        Assert.Null(await tab.Sessions.GetCurrent());
        Assert.Empty(await EventsKeptForAsync(ana));
        Assert.DoesNotContain(asked.Asked, request => request.StartsWith("POST") || request.StartsWith("PATCH"));
    }

    [Fact]
    public async Task What_a_person_selected_is_kept_and_comes_back_in_another_tab()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var ana = await SignedInAsync(api, client);
        var eventId = Guid.NewGuid();
        using var first = Open(api, ana, out _);
        await first.Sessions.SetEventId(eventId);
        await first.Sessions.ReplaceSnapshotSelections(
            [Selected(7, "10:15:30"), Selected(12, ruleset: CompetitionRuleset.Regional)]
        );
        using var second = Open(api, ana, out _);
        await second.Sessions.SetEventId(eventId);

        var state = await second.Sessions.GetCurrent();

        Assert.Equal(
            ["7|Rider 7|Rider 7 in English||10:15:30", "12|Rider 12|Rider 12 in English|Regional|"],
            Describe(state!.GetSnapshotSelections())
        );
        Assert.Equal([eventId], await EventsKeptForAsync(ana));
    }

    [Fact]
    public async Task Replacing_the_selections_replaces_them_all_and_an_empty_list_takes_them_all_away()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var ana = await SignedInAsync(api, client);
        using var tab = Open(api, ana, out _);
        await tab.Sessions.SetEventId(Guid.NewGuid());
        await tab.Sessions.ReplaceSnapshotSelections([Selected(7), Selected(12)]);

        await tab.Sessions.ReplaceSnapshotSelections([Selected(12)]);
        var narrowed = (await tab.Sessions.GetCurrent())!.GetSnapshotSelections().Select(x => x.Number);
        await tab.Sessions.ReplaceSnapshotSelections([]);
        var emptied = (await tab.Sessions.GetCurrent())!.GetSnapshotSelections();

        Assert.Equal([12], narrowed);
        Assert.Empty(emptied);
    }

    [Fact]
    public async Task A_group_that_is_sent_goes_to_the_history_and_its_riders_leave_the_selections()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var ana = await SignedInAsync(api, client);
        using var tab = Open(api, ana, out _);
        await tab.Sessions.SetEventId(Guid.NewGuid());
        await tab.Sessions.ReplaceSnapshotSelections([Selected(7, "10:00:00"), Selected(12, "10:05:00")]);

        await tab.Sessions.AppendSnapshot(Sent(SnapshotType.Arrive, 7));

        var state = (await tab.Sessions.GetCurrent())!;
        var group = Assert.Single(state.GetSnapshotHistory());
        Assert.Equal(SnapshotType.Arrive, group.Type);
        Assert.Equal(["7|Rider 7|Rider 7 in English||10:00:00"], Describe(group.Entries));
        Assert.Equal([12], state.GetSnapshotSelections().Select(x => x.Number));
    }

    [Fact]
    public async Task Groups_sent_one_after_another_are_all_kept_in_the_order_they_were_sent()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var ana = await SignedInAsync(api, client);
        using var tab = Open(api, ana, out _);
        await tab.Sessions.SetEventId(Guid.NewGuid());

        await tab.Sessions.AppendSnapshot(Sent(SnapshotType.Present, 7));
        await tab.Sessions.AppendSnapshot(Sent(SnapshotType.Arrive, 7, 12));
        await tab.Sessions.AppendSnapshot(Sent(SnapshotType.Final, 12));

        var history = (await tab.Sessions.GetCurrent())!.GetSnapshotHistory();
        Assert.Equal(
            [(SnapshotType.Present, 1), (SnapshotType.Arrive, 2), (SnapshotType.Final, 1)],
            history.Select(x => (x.Type, x.Entries.Count()))
        );
    }

    [Fact]
    public async Task A_group_sent_after_the_record_was_deleted_makes_the_record_again()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var ana = await SignedInAsync(api, client);
        var eventId = Guid.NewGuid();
        using var tab = Open(api, ana, out _);
        await tab.Sessions.SetEventId(eventId);
        await tab.Sessions.ReplaceSnapshotSelections([Selected(7, "10:00:00")]);
        await tab.Sessions.DeleteCurrent();
        Assert.Empty(await EventsKeptForAsync(ana));

        await tab.Sessions.AppendSnapshot(Sent(SnapshotType.Present, 7));

        Assert.Equal([eventId], await EventsKeptForAsync(ana));
        Assert.Single((await tab.Sessions.GetCurrent())!.GetSnapshotHistory());
    }

    [Fact]
    public async Task Deleting_removes_what_the_person_kept_for_the_Event_and_deleting_nothing_is_nothing()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var ana = await SignedInAsync(api, client);
        using var tab = Open(api, ana, out _);
        await tab.Sessions.SetEventId(Guid.NewGuid());
        await tab.Sessions.ReplaceSnapshotSelections([Selected(7)]);

        await tab.Sessions.DeleteCurrent();
        await tab.Sessions.DeleteCurrent();

        Assert.Null(await tab.Sessions.GetCurrent());
        Assert.Empty(await EventsKeptForAsync(ana));
    }

    [Fact]
    public async Task Nothing_is_kept_or_read_before_an_Event_is_named_and_an_Event_not_named_leaves_the_one_that_was()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var ana = await SignedInAsync(api, client);
        var eventId = Guid.NewGuid();
        using var tab = Open(api, ana, out var asked);

        await tab.Sessions.ReplaceSnapshotSelections([Selected(7)]);
        await tab.Sessions.AppendSnapshot(Sent(SnapshotType.Present, 7));
        await tab.Sessions.DeleteCurrent();
        Assert.Null(await tab.Sessions.GetCurrent());
        Assert.DoesNotContain(asked.Asked, request => request.Contains("user-sessions"));

        await tab.Sessions.SetEventId(eventId);
        await tab.Sessions.ReplaceSnapshotSelections([Selected(7)]);
        await tab.Sessions.SetEventId(null);
        await tab.Sessions.ReplaceSnapshotSelections([Selected(7), Selected(12)]);

        Assert.Equal([eventId], await EventsKeptForAsync(ana));
        Assert.Equal([7, 12], (await tab.Sessions.GetCurrent())!.GetSnapshotSelections().Select(x => x.Number));
    }

    [Fact]
    public async Task Each_Event_has_a_state_of_its_own()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var ana = await SignedInAsync(api, client);
        var anEvent = Guid.NewGuid();
        var another = Guid.NewGuid();
        using var tab = Open(api, ana, out _);
        await tab.Sessions.SetEventId(anEvent);
        await tab.Sessions.ReplaceSnapshotSelections([Selected(7)]);

        await tab.Sessions.SetEventId(another);
        var inTheOther = await tab.Sessions.GetCurrent();
        await tab.Sessions.ReplaceSnapshotSelections([Selected(8)]);
        await tab.Sessions.SetEventId(anEvent);

        Assert.Null(inTheOther);
        Assert.Equal([7], (await tab.Sessions.GetCurrent())!.GetSnapshotSelections().Select(x => x.Number));
        Assert.Equal(new[] { anEvent, another }.Order(), (await EventsKeptForAsync(ana)).Order());
    }

    [Fact]
    public async Task The_state_of_one_person_is_never_the_state_of_another_for_the_same_Event()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var ana = await SignedInAsync(api, client);
        var boris = await SignedInAsync(api, client);
        var eventId = Guid.NewGuid();
        using var anas = Open(api, ana, out _);
        using var boriss = Open(api, boris, out _);
        await anas.Sessions.SetEventId(eventId);
        await boriss.Sessions.SetEventId(eventId);

        await anas.Sessions.ReplaceSnapshotSelections([Selected(7)]);
        await boriss.Sessions.ReplaceSnapshotSelections([Selected(12)]);
        await anas.Sessions.AppendSnapshot(Sent(SnapshotType.Present, 7));
        await boriss.Sessions.DeleteCurrent();

        var stateOfAna = (await anas.Sessions.GetCurrent())!;
        Assert.Single(stateOfAna.GetSnapshotHistory());
        Assert.Empty(stateOfAna.GetSnapshotSelections());
        Assert.Null(await boriss.Sessions.GetCurrent());
    }

    [Fact]
    public async Task A_person_whose_session_ended_elsewhere_is_a_visitor_again_and_nothing_is_thrown()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var ana = await SignedInAsync(api, client);
        using var tab = Open(api, ana, out _);
        await tab.Sessions.SetEventId(Guid.NewGuid());
        await tab.Sessions.ReplaceSnapshotSelections([Selected(7)]);
        Assert.True(tab.Account.IsSignedIn);
        await ana.Page.DeleteAsync("/api/sessions/current"); // another tab, or another device, signed out

        var state = await tab.Sessions.GetCurrent();
        await tab.Sessions.ReplaceSnapshotSelections([Selected(7), Selected(12)]);

        Assert.Null(state);
        Assert.False(tab.Account.IsSignedIn); // the account was told, and the app shows a visitor
    }

    [Fact]
    public async Task A_host_that_fails_is_told_apart_from_a_person_who_has_kept_nothing()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var ana = await SignedInAsync(api, client);
        using var tab = Open(api, ana, out var asked);
        await tab.Sessions.SetEventId(Guid.NewGuid());
        await tab.Sessions.ReplaceSnapshotSelections([Selected(7)]);
        asked.Answer = request =>
            request.RequestUri!.AbsolutePath.Contains("user-sessions")
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : null;

        var read = await Assert.ThrowsAsync<InvalidOperationException>(() => tab.Sessions.GetCurrent());
        var written = await Assert.ThrowsAsync<InvalidOperationException>(
            () => tab.Sessions.ReplaceSnapshotSelections([Selected(12)])
        );

        Assert.Contains("500", read.Message);
        Assert.Contains("500", written.Message);
        Assert.True(tab.Account.IsSignedIn);
        asked.Answer = null;
        Assert.Equal([7], (await tab.Sessions.GetCurrent())!.GetSnapshotSelections().Select(x => x.Number));
    }

    [Fact]
    public async Task A_host_that_cannot_say_who_is_signed_in_is_not_taken_for_a_visitor_with_nothing_to_keep()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var ana = await SignedInAsync(api, client);
        using var tab = Open(api, ana, out var asked);
        await tab.Sessions.SetEventId(Guid.NewGuid());
        asked.Answer = request =>
            request.RequestUri!.AbsolutePath.EndsWith("/me")
                ? new HttpResponseMessage(HttpStatusCode.BadGateway)
                : null;

        await Assert.ThrowsAsync<InvalidOperationException>(() => tab.Sessions.GetCurrent());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => tab.Sessions.ReplaceSnapshotSelections([Selected(7)])
        );

        Assert.Empty(await EventsKeptForAsync(ana)); // nothing was dropped on the way, and nothing was kept under nobody
        asked.Answer = null;
        await tab.Sessions.ReplaceSnapshotSelections([Selected(7)]);
        Assert.Equal([7], (await tab.Sessions.GetCurrent())!.GetSnapshotSelections().Select(x => x.Number));
    }

    static Snapshot Selected(int number, string? time = null, CompetitionRuleset? ruleset = null)
    {
        return new Snapshot(
            number,
            $"Rider {number}",
            $"Rider {number} in English",
            time == null ? null : new Timestamp(time),
            ruleset
        );
    }

    static SnapshotGroup Sent(SnapshotType type, params int[] numbers)
    {
        return new SnapshotGroup(numbers.Select(number => Selected(number, "10:00:00")), type);
    }

    static string[] Describe(IEnumerable<Snapshot> snapshots)
    {
        return [.. snapshots.Select(x => $"{x.Number}|{x.Name}|{x.NameEnglish}|{x.Ruleset}|{x.Timestamp}")];
    }

    static Tab Open(ApiFactory api, TenancySeed.Person? person, out JsonApiClients.Requests asked)
    {
        var json = JsonApiClients.Of(api, person, out asked);
        var account = new AccountSession(json);
        return new Tab(account, new WitnessUserSessionService(account, json));
    }

    /// <summary>The Events that the person has kept something for, as the Api lists them for the person.</summary>
    static async Task<List<Guid>> EventsKeptForAsync(TenancySeed.Person person)
    {
        var response = await person.Page.GetAsync("/api/user-sessions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data");
        return
        [
            .. data.EnumerateArray()
                .Select(x => Guid.Parse(x.GetProperty("attributes").GetProperty("eventId").GetString()!)),
        ];
    }

    async Task<TenancySeed.Person> SignedInAsync(ApiFactory api, HttpClient client)
    {
        return await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, null);
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
