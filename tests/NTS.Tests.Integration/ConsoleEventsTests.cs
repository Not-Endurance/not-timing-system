using Not.Application.DomainEvents;
using Not.Domain;
using Not.Domain.Abstractions;
using Not.Domain.Exceptions;
using NoTiming.Ui.Features.Account;
using NoTiming.Ui.Features.Console;
using NoTiming.Ui.Storage.Core.Repositories;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Features.Console;
using NTS.Domain.Access;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The Events of the Console's home (#646, ADR-0012): the Setups a person sees at their stage and what they may do about
/// each, which is what the Api says and not what the Ui works out; making one, assigning and handing over its Main Operator,
/// starting it and resetting it. Over the real Api in this process.
/// </summary>
public sealed class ConsoleEventsTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset NOW = DateTimeOffset.UtcNow;

    readonly MongoFixture _mongo;

    public ConsoleEventsTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_Tenant_Root_sees_every_Setup_of_the_Tenant_at_its_stage_and_may_make_one_but_runs_none_of_them()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var open = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id, "Open");
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, NOW);
        var historic = await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, mainOperator.Id, NOW);
        using var console = Open(api, root);

        await console.Events.Load();

        Assert.True(console.Events.CanCreate);
        var rows = console.Events.Events.ToDictionary(x => x.Id);
        Assert.Equal(EventStage.Unstarted, rows[open].Stage);
        Assert.Equal(EventStage.Live, rows[live].Stage);
        Assert.Equal(EventStage.Historic, rows[historic].Stage);
        Assert.All(rows.Values, x => Assert.False(x.IsMainOperator)); // it runs none of them: it assigns itself first
        Assert.True(rows[open].CanAssignMainOperator);
        Assert.False(rows[live].CanAssignMainOperator);
        Assert.All(rows.Values, x => Assert.False(x.CanConfigure || x.CanEditData || x.CanReset || x.CanHandOver));
    }

    [Fact]
    public async Task The_Main_Operator_sees_the_Events_it_runs_and_may_configure_the_one_that_has_not_started_and_hand_over_or_reset_the_Live_one()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var somebodyElse = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var open = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id, "Open");
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, NOW);
        var notMine = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, somebodyElse.Id, "Not mine");
        using var console = Open(api, mainOperator);

        await console.Events.Load();

        var rows = console.Events.Events.ToDictionary(x => x.Id);
        Assert.Equal(2, rows.Count); // what it runs, and not what it does not
        Assert.Contains(open, rows.Keys);
        Assert.Contains(live, rows.Keys);
        Assert.DoesNotContain(notMine, rows.Keys);
        Assert.False(console.Events.CanCreate); // a Main Operator who is not a Tenant Root makes no Event
        Assert.True(rows[open].IsMainOperator);
        Assert.True(rows[open].CanConfigure && rows[open].CanEditSetup && rows[open].CanLinkAccounts);
        Assert.False(rows[open].CanReset || rows[open].CanHandOver || rows[open].CanEditData);
        Assert.True(rows[live].CanReset && rows[live].CanHandOver && rows[live].CanEditData);
        Assert.False(rows[live].CanEditSetup); // the Setup is not edited once the Event started
    }

    [Fact]
    public async Task A_Tenant_Root_makes_an_Event_that_it_runs_and_assigns_it_to_an_account_found_by_name_while_it_has_not_started()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var next = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, name: "Boris Ivanov");
        using var console = Open(api, root);
        await console.Events.Load();

        var made = await console.Events.Create("The Cup", "Kaspichan", "FEI42");
        var asked = made.IsMainOperator;
        var found = await console.Events.FindAccounts("Boris Iv");
        await console.Events.AssignMainOperator(made.Id, found.Single().Id);
        await console.Events.Load();

        Assert.Equal("The Cup", made.Name);
        Assert.Equal(EventStage.Unstarted, made.Stage);
        Assert.True(asked); // the Tenant Root who made it runs it, until it assigns it away
        Assert.Equal(next.Id, found.Single().Id);
        var after = console.Events.Events.Single(x => x.Id == made.Id);
        Assert.False(after.IsMainOperator); // and having assigned it, it does not
        using var theirs = Open(api, next);
        await theirs.Events.Load();
        Assert.True(theirs.Events.Events.Single(x => x.Id == made.Id).IsMainOperator);
    }

    [Fact]
    public async Task The_Main_Operator_of_a_Live_Event_hands_it_over_to_another_account_which_then_runs_it()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var next = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        using var first = Open(api, mainOperator);
        await first.Events.Load();
        await first.Events.Start(id);

        await first.Events.HandOver(id, next.Id);
        await first.Events.Load();
        using var second = Open(api, next);
        await second.Events.Load();

        Assert.DoesNotContain(id, first.Events.Events.Select(x => x.Id)); // it does not run it, and it is not in its list
        var theirs = second.Events.Events.Single(x => x.Id == id);
        Assert.Equal(EventStage.Live, theirs.Stage);
        Assert.True(theirs.IsMainOperator && theirs.CanReset && theirs.CanHandOver);
    }

    [Fact]
    public async Task The_Main_Operator_resets_a_Live_Event_to_its_Setup_which_can_be_configured_again()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        using var console = Open(api, mainOperator);
        await console.Events.Load();
        await console.Events.Start(id);
        await console.Events.Load();
        var live = console.Events.Events.Single(x => x.Id == id);

        await console.Events.Reset(id);
        await console.Events.Load();

        var back = console.Events.Events.Single(x => x.Id == id);
        Assert.Equal(EventStage.Live, live.Stage);
        Assert.Equal(EventStage.Unstarted, back.Stage);
        Assert.True(back.CanEditSetup);
    }

    [Fact]
    public async Task Starting_a_Setup_that_is_whole_makes_the_Event_Live_and_one_that_is_not_says_what_is_missing_and_starts_nothing()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var whole = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        var empty = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id, "Empty");
        using var console = Open(api, mainOperator);
        await console.Events.Load();

        var issues = await console.Events.Validate(empty);
        var refused = await Assert.ThrowsAsync<DomainException>(() => console.Events.Start(empty));
        await console.Events.Start(whole);
        await console.Events.Load();

        Assert.NotEmpty(issues);
        Assert.Empty(await console.Events.Validate(whole));
        Assert.Contains("competition", refused.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(EventStage.Live, console.Events.Events.Single(x => x.Id == whole).Stage);
        Assert.Equal(EventStage.Unstarted, console.Events.Events.Single(x => x.Id == empty).Stage);
    }

    [Fact]
    public async Task An_Operator_an_Official_and_the_Developer_run_none_of_the_Events_they_may_see()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var operatorGrant = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var official = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, NOW);
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            tenant,
            live,
            "Operator",
            null,
            operatorGrant.Email,
            operatorGrant.Id
        );
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            tenant,
            live,
            "Official",
            "Steward",
            official.Email,
            official.Id
        );

        foreach (var person in new[] { operatorGrant, official, developer })
        {
            using var console = Open(api, person);
            await console.Events.Load();

            Assert.All(console.Events.Events, x => Assert.False(x.IsMainOperator || x.CanEditData || x.CanReset));
        }
    }

    static Opened Open(ApiFactory api, Person person)
    {
        var json = JsonApiClients.Of(api, person, out _);
        var account = new AccountSession(json);
        var setups = new ConfigureEventApiRepository(json, new NoDispatcher());
        var events = new EventInformationApiRepository(json, new SelectedEvent(null));
        return new Opened(account, new ConsoleEvents(account, setups, events, json));
    }

    sealed class Opened : IDisposable
    {
        public Opened(AccountSession account, ConsoleEvents events)
        {
            Account = account;
            Events = events;
        }

        public AccountSession Account { get; }
        public ConsoleEvents Events { get; }

        public void Dispose()
        {
            Events.Dispose();
            Account.Dispose();
        }
    }

    sealed class NoDispatcher : IDomainEventDispatcher
    {
        public Task Dispatch(IDomainEvent @event, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task Dispatch(Aggregate aggregate, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
