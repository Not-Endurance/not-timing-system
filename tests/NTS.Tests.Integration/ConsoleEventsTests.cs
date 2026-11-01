using MongoDB.Bson;
using MongoDB.Driver;
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
/// The Events of the Console's home (#646, ADR-0012, #671): the Setups a person sees at their stage and what they may do about
/// each, which is what the Api says and not what the Ui works out; making one, assigning and removing its Console Operators,
/// starting it and resetting it. Root runs every Event of the Tenant, and a Console Operator runs the Events it is assigned.
/// Over the real Api in this process.
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
    public async Task A_Tenant_Root_sees_every_Setup_of_the_Tenant_at_its_stage_and_runs_every_one_of_them_as_the_stage_allows()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var open = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, null, "Open");
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, NOW);
        var historic = await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, null, NOW);
        using var console = Open(api, root);

        await console.Events.Load();

        Assert.True(console.Events.CanCreate);
        var rows = console.Events.Events.ToDictionary(x => x.Id);
        Assert.Equal(EventStage.Unstarted, rows[open].Stage);
        Assert.Equal(EventStage.Live, rows[live].Stage);
        Assert.Equal(EventStage.Historic, rows[historic].Stage);
        Assert.All(rows.Values, x => Assert.Equal(AccessLevel.Root, x.Level)); // nobody was assigned, and it runs them all
        Assert.All(rows.Values, x => Assert.True(x.RunsTheConsole));
        Assert.Equal(
            [true, true, false],
            [rows[open].CanConfigure, rows[live].CanConfigure, rows[historic].CanConfigure]
        );
        Assert.Equal(
            [true, false, false],
            [rows[open].CanEditSetup, rows[live].CanEditSetup, rows[historic].CanEditSetup]
        );
        Assert.Equal([false, true, false], [rows[open].CanRun, rows[live].CanRun, rows[historic].CanRun]);
        Assert.Equal(
            [true, true, false],
            [
                rows[open].CanAssignConsoleOperators,
                rows[live].CanAssignConsoleOperators,
                rows[historic].CanAssignConsoleOperators,
            ]
        );
    }

    [Fact]
    public async Task The_Console_Operator_sees_the_Events_it_runs_and_may_configure_the_one_that_has_not_started_and_run_or_reset_the_Live_one()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var somebodyElse = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var open = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, "Open");
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, NOW);
        var notMine = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, somebodyElse.Id, "Not mine");
        using var console = Open(api, consoleOperator);

        await console.Events.Load();

        var rows = console.Events.Events.ToDictionary(x => x.Id);
        Assert.Equal(2, rows.Count); // what it runs, and not what it does not
        Assert.Contains(open, rows.Keys);
        Assert.Contains(live, rows.Keys);
        Assert.DoesNotContain(notMine, rows.Keys);
        Assert.False(console.Events.CanCreate); // a Console Operator who is not Root makes no Event
        Assert.All(rows.Values, x => Assert.Equal(AccessLevel.ConsoleOperator, x.Level));
        Assert.True(rows[open].CanConfigure && rows[open].CanEditSetup);
        Assert.False(rows[open].CanRun);
        Assert.True(rows[live].CanRun);
        Assert.True(rows[live].CanConfigure); // what is set up for the Event is seen to until it ends
        Assert.False(rows[live].CanEditSetup); // the Setup is not edited once the Event started
        Assert.All(rows.Values, x => Assert.False(x.CanAssignConsoleOperators)); // only Root assigns them
    }

    [Fact]
    public async Task A_Tenant_Root_makes_an_Event_that_it_runs_and_assigns_a_Console_Operator_found_by_name_who_then_runs_it_too()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var next = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, name: "Boris Ivanov");
        using var console = Open(api, root);
        await console.Events.Load();

        var made = await console.Events.Create("The Cup", "Kaspichan", "FEI42");
        var before = await console.Events.ConsoleOperators(made.Id);
        var found = await console.Events.FindAccounts("Boris Iv");
        await console.Events.AssignConsoleOperator(made.Id, found.Single().Id);
        var assigned = await console.Events.ConsoleOperators(made.Id);
        await console.Events.Load();

        Assert.Equal("The Cup", made.Name);
        Assert.Equal(EventStage.Unstarted, made.Stage);
        Assert.Equal(AccessLevel.Root, made.Level); // the Tenant Root runs what it made
        Assert.Empty(before); // and nobody is assigned when an Event is made
        Assert.Equal(next.Id, found.Single().Id);
        var listed = Assert.Single(assigned);
        Assert.False(listed.IsPending);
        Assert.Equal("Boris Ivanov", listed.DisplayName);
        Assert.NotEqual(next.Email, listed.Email); // the address is told partly masked
        Assert.Equal(AccessLevel.Root, console.Events.Events.Single(x => x.Id == made.Id).Level); // and it still does
        using var theirs = Open(api, next);
        await theirs.Events.Load();
        var row = theirs.Events.Events.Single(x => x.Id == made.Id);
        Assert.Equal(AccessLevel.ConsoleOperator, row.Level);
        Assert.True(row.CanConfigure && row.CanEditSetup);
    }

    [Fact]
    public async Task A_Live_Event_is_run_by_Root_and_by_every_Console_Operator_assigned_to_it_at_once()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var first = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var second = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, first.Id, NOW);
        using var console = Open(api, root);
        await console.Events.Load();

        await console.Events.AssignConsoleOperator(live, second.Id);

        Assert.Equal(2, (await console.Events.ConsoleOperators(live)).Count);
        foreach (var person in new[] { root, first, second })
        {
            using var theirs = Open(api, person);
            await theirs.Events.Load();
            var row = theirs.Events.Events.Single(x => x.Id == live);
            Assert.True(row.RunsTheConsole && row.CanRun);
        }
    }

    [Fact]
    public async Task A_removed_Console_Operator_no_longer_runs_the_Event_and_the_Api_says_so_to_one_that_is_not_there()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, NOW);
        using var console = Open(api, root);
        await console.Events.Load();
        var assigned = Assert.Single(await console.Events.ConsoleOperators(live));

        await console.Events.RemoveConsoleOperator(assigned.Id);
        var gone = await Assert.ThrowsAsync<DomainException>(() => console.Events.RemoveConsoleOperator(assigned.Id));

        Assert.Empty(await console.Events.ConsoleOperators(live));
        Assert.NotEmpty(gone.Message);
        using var theirs = Open(api, consoleOperator);
        await theirs.Events.Load();
        Assert.Empty(theirs.Events.Events);
    }

    [Fact]
    public async Task An_account_is_found_by_a_name_that_has_a_quote_in_it()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var conan = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, name: "Conan O'Brien");
        using var console = Open(api, root);
        await console.Events.Load();

        var found = await console.Events.FindAccounts("O'Bri");

        Assert.Equal([conan.Id], found.Select(x => x.Id));
    }

    [Fact]
    public async Task A_text_with_no_word_of_two_characters_finds_no_account_and_is_not_sent_and_one_of_many_words_is_cut_to_six()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var wu = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, name: "Li Wu Konstantin");
        using var console = Open(api, root, out var requests);
        await console.Events.Load();
        int Searches() => requests.Asked.Count(x => x.Contains("/api/accounts", StringComparison.Ordinal));

        var typed = await console.Events.FindAccounts("L W"); // what a person has typed of the name so far: no word of two characters
        var asked = Searches();
        var sevenWords = await console.Events.FindAccounts("li wu kon s t a n"); // the Api takes six words and the seventh is left out

        Assert.Empty(typed);
        Assert.Equal(0, asked);
        Assert.Equal([wu.Id], sevenWords.Select(x => x.Id));
    }

    [Fact]
    public async Task Root_cannot_assign_a_Console_Operator_to_an_Event_that_has_ended_and_a_Console_Operator_cannot_assign_one_at_all()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var other = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var historic = await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, NOW);
        var open = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, "Open");
        using var byRoot = Open(api, root);
        using var byConsoleOperator = Open(api, consoleOperator);
        await byRoot.Events.Load();
        await byConsoleOperator.Events.Load();

        var ended = await Assert.ThrowsAsync<DomainException>(
            () => byRoot.Events.AssignConsoleOperator(historic, other.Id)
        );
        var notRoot = await Assert.ThrowsAsync<DomainException>(
            () => byConsoleOperator.Events.AssignConsoleOperator(open, other.Id)
        );

        Assert.NotEmpty(ended.Message);
        Assert.NotEmpty(notRoot.Message);
        Assert.Equal(1, await ConsoleOperatorsOfAsync(historic)); // the one who ran it, and nobody was added
        Assert.Equal(1, await ConsoleOperatorsOfAsync(open));
    }

    [Fact]
    public async Task What_the_Console_lists_is_gone_when_the_session_ends()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, null, "Open");
        using var console = Open(api, root);
        await console.Events.Load();
        var before = (console.Events.Events.Count, console.Events.CanCreate, console.Events.CanEditRegistry);

        await console.Account.SignOut();

        Assert.Equal((1, true, true), before);
        await UiBrowser.Eventually(async () => console.Events.Events.Count == 0);
        Assert.False(console.Events.CanCreate);
        Assert.False(console.Events.CanEditRegistry);
    }

    [Fact]
    public async Task The_Events_are_listed_by_name_whatever_the_case_and_one_that_is_not_there_is_not_validated()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        foreach (var name in new[] { "delta", "Alpha", "charlie", "Bravo", "echo" })
        {
            await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, name);
        }

        using var console = Open(api, consoleOperator);
        await console.Events.Load();

        Assert.Equal(["Alpha", "Bravo", "charlie", "delta", "echo"], console.Events.Events.Select(x => x.Name));
        var missing = await Assert.ThrowsAsync<DomainException>(() => console.Events.Validate(Guid.NewGuid()));
        Assert.Equal(NTS.Localization.NtsStrings.Event_not_found_string, missing.Message);
    }

    [Fact]
    public async Task The_Console_Operator_resets_a_Live_Event_to_its_Setup_which_can_be_configured_again()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id);
        using var console = Open(api, consoleOperator);
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
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var whole = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id);
        var empty = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, "Empty");
        using var console = Open(api, consoleOperator);
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
    public async Task An_Operator_and_an_Official_see_no_Event_to_run_and_the_Developer_runs_the_Events_of_the_Tenant_it_acts_in()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var operatorGrant = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var official = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, NOW);
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

        foreach (var person in new[] { operatorGrant, official })
        {
            using var console = Open(api, person);
            await console.Events.Load();

            Assert.Empty(console.Events.Events);
        }

        using var asDeveloper = Open(api, developer);
        await asDeveloper.Events.Load();
        var row = asDeveloper.Events.Events.Single(x => x.Id == live);
        Assert.Equal(AccessLevel.Root, row.Level);
        Assert.True(row.RunsTheConsole && row.CanRun);
    }

    [Fact]
    public async Task The_registry_is_open_for_writing_to_a_Tenant_Root_and_to_the_Console_Operator_of_an_Event_that_is_not_over_and_to_nobody_else()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var running = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var past = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var plain = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, running.Id, "Open");
        await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, past.Id, NOW);

        var allowed = new Dictionary<string, bool>();
        foreach (var (name, person) in new[] { ("root", root), ("running", running), ("past", past), ("plain", plain) })
        {
            using var console = Open(api, person);
            await console.Events.Load();
            allowed[name] = console.Events.CanEditRegistry;
        }

        Assert.True(allowed["root"]);
        Assert.True(allowed["running"]);
        Assert.False(allowed["past"]); // an Event that is over is not one to register for
        Assert.False(allowed["plain"]);
    }

    /// <summary>How many Console Operators the Event has, as the collection of grants keeps them.</summary>
    async Task<long> ConsoleOperatorsOfAsync(Guid eventId)
    {
        return await EventSeed
            .Grants(_mongo.ConnectionString)
            .CountDocumentsAsync(
                new BsonDocument { { "EventId", EventSeed.Binary(eventId) }, { "Kind", "ConsoleOperator" } }
            );
    }

    static Opened Open(ApiFactory api, Person person)
    {
        return Open(api, person, out _);
    }

    static Opened Open(ApiFactory api, Person person, out JsonApiClients.Requests requests)
    {
        var json = JsonApiClients.Of(api, person, out requests);
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
