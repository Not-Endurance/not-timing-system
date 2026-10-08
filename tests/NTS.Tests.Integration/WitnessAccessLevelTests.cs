using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Ui.Features.Access;
using NoTiming.Ui.Features.Account;
using NTS.Contracts.Features.Access;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// What the Witness shows of a person who may send Snapshots (#645): their level does not dip to Registered while the Api is
/// asked again what they may do (a page that decides by it, such as the Snapshot page, would send an Official away for the
/// time it takes), and it does change when the Api says they may not. Over the real Api in this process.
/// </summary>
public sealed class WitnessAccessLevelTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public WitnessAccessLevelTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task The_level_of_a_person_who_may_send_Snapshots_does_not_dip_while_the_Api_is_asked_again()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var (eventId, official) = await SceneAsync(api, client);
        var json = JsonApiClients.Of(api, official, out var asked);
        using var account = new AccountSession(json);
        using var access = new WitnessAccessContext(new SelectedEvent(eventId), account, json);
        await access.Load();
        Assert.Equal(WitnessAccessLevel.Official, access.AccessLevel);
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var told = new List<WitnessAccessLevel>();
        access.ObservableEvent.Subscribe(() => told.Add(access.AccessLevel));
        asked.Before = request =>
            request.RequestUri!.AbsolutePath.EndsWith("/capabilities") ? held.Task : Task.CompletedTask;
        var before = CapabilityRequests(asked);

        await account.Refresh(); // the account says the same, and the Api is asked what the person may do again
        await Eventually(() => CapabilityRequests(asked) > before);

        Assert.Equal(WitnessAccessLevel.Official, access.AccessLevel); // while the question is on its way
        held.SetResult();
        await Eventually(() => told.Count > 0);
        Assert.Equal(WitnessAccessLevel.Official, access.AccessLevel);
        Assert.DoesNotContain(WitnessAccessLevel.Registered, told);
    }

    [Fact]
    public async Task The_level_changes_when_the_Api_says_the_person_may_no_longer_send_Snapshots()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var (eventId, official) = await SceneAsync(api, client);
        var json = JsonApiClients.Of(api, official, out _);
        using var account = new AccountSession(json);
        using var access = new WitnessAccessContext(new SelectedEvent(eventId), account, json);
        await access.Load();
        Assert.Equal(WitnessAccessLevel.Official, access.AccessLevel);
        await EventSeed.Grants(_mongo.ConnectionString).DeleteManyAsync(new BsonDocument("Email", official.Email));

        await account.Refresh();

        await Eventually(() => access.AccessLevel == WitnessAccessLevel.Registered);
    }

    static int CapabilityRequests(JsonApiClients.Requests asked)
    {
        lock (asked.Asked)
        {
            return asked.Asked.Count(x => x.Contains("/capabilities"));
        }
    }

    async Task<(Guid EventId, TenancySeed.Person Official)> SceneAsync(ApiFactory api, HttpClient client)
    {
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var official = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            tenant,
            eventId,
            "Official",
            "Steward",
            official.Email,
            official.Id
        );
        return (eventId, official);
    }

    static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!condition() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "The Witness did not do what was waited for.");
    }
}
