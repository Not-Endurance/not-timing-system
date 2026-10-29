using Not.Domain.Exceptions;
using Not.Krud.Abstractions;
using NTS.Contracts.Features.Console;
using NTS.Contracts.Features.Console.Setup;
using NTS.Domain.Access;
using NTS.Domain.Enums;
using NTS.Tests.Integration.Harness;
using static NTS.Tests.Integration.Harness.TenancySeed;
using SetupAthlete = NTS.Domain.Setup.Aggregates.Athlete;
using SetupCombination = NTS.Domain.Setup.Aggregates.ConfigureEvents.Combination;
using SetupCompetition = NTS.Domain.Setup.Aggregates.ConfigureEvents.Competition;
using SetupHorse = NTS.Domain.Setup.Aggregates.Horse;
using SetupLoop = NTS.Domain.Setup.Aggregates.ConfigureEvents.Loop;

namespace NTS.Tests.Integration;

/// <summary>
/// The Setup of an Event as the Console edits it (#646, ADR-0012): opened by the Console Operator of the Event and by nobody
/// else, composed of its Competitions, Phases, Loops, Combinations and Participations through the services the Krud pages
/// use, and stored by the Api each time. Over the real Api in this process and the container the app builds.
/// </summary>
public sealed class ConsoleSetupTests : IClassFixture<SharedApiFixture>
{
    readonly SharedApiFixture _shared;

    public ConsoleSetupTests(SharedApiFixture shared)
    {
        _shared = shared;
    }

    [Fact]
    public async Task A_Console_Operator_of_an_Event_and_Root_open_its_Setup_and_an_Operator_or_an_Official_does_not()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var root = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant, TenantRootOf(tenant));
        var operatorGrant = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var official = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var developer = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant, isDeveloper: true);
        var id = await EventSeed.SetupAsync(_shared.MongoConnectionString, tenant, consoleOperator.Id, "Open");
        await EventSeed.GrantAsync(
            _shared.MongoConnectionString,
            tenant,
            id,
            "Operator",
            null,
            operatorGrant.Email,
            operatorGrant.Id
        );
        await EventSeed.GrantAsync(
            _shared.MongoConnectionString,
            tenant,
            id,
            "Official",
            "Steward",
            official.Email,
            official.Id
        );

        foreach (var person in new[] { operatorGrant, official })
        {
            await using var theirs = ConsoleUi.Open(api, person);

            Assert.Null(await theirs.Get<IConsoleSetup>().Open(id));
        }

        foreach (var person in new[] { consoleOperator, root, developer })
        {
            await using var mine = ConsoleUi.Open(api, person);
            var opened = await mine.Get<IConsoleSetup>().Open(id);
            Assert.NotNull(opened);
            Assert.Equal("Open", opened.Model.Name);
            Assert.True(opened.CanEdit && opened.CanLinkAccounts);
        }
    }

    [Fact]
    public async Task A_Setup_that_is_composed_through_the_services_of_the_pages_is_stored_whole_and_starts()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var id = await EventSeed.SetupAsync(_shared.MongoConnectionString, tenant, consoleOperator.Id, "The Cup");
        await using var ui = ConsoleUi.Open(api, consoleOperator);
        var setup = ui.Get<IConsoleSetup>();
        var opened = await setup.Open(id);
        Assert.NotNull(opened);

        // the registry, which the Combination picks from
        var country = (await ui.Get<IConsoleLookups>().TenantCountry())!;
        await ui.Get<IKrudFormService<AthleteFormModel>>()
            .Create(new AthleteFormModel { Name = "Ivan Petrov", Country = country });
        await ui.Get<IKrudFormService<HorseFormModel>>().Create(new HorseFormModel { Name = "Barza" });

        // what the lists of the Setup make
        await ui.Get<IKrudFormService<LoopFormModel>>().Create(new LoopFormModel { Distance = 40 });
        await ui.Get<IKrudFormService<LoopFormModel>>().Create(new LoopFormModel { Distance = 25 });
        await ui.Get<IKrudFormService<CompetitionFormModel>>()
            .Create(
                new CompetitionFormModel
                {
                    Name = "CEI 1*",
                    Ruleset = CompetitionRuleset.Regional,
                    Date = new DateTime(2031, 5, 3),
                    Time = TimeSpan.FromHours(8),
                }
            );
        var athlete = (await ui.Get<IKrudListBehind<SetupAthlete>>().ReadMany()).Single();
        var horse = (await ui.Get<IKrudListBehind<SetupHorse>>().ReadMany()).Single();
        await ui.Get<IKrudFormService<CombinationFormModel>>()
            .Create(
                new CombinationFormModel
                {
                    Number = 7,
                    Athlete = athlete,
                    Horse = horse,
                }
            );
        var loops = (await ui.Get<IKrudListBehind<SetupLoop>>().ReadMany()).OrderByDescending(x => x.Distance).ToList();
        var combination = (await ui.Get<IKrudListBehind<SetupCombination>>().ReadMany()).Single();

        // and what the page of the Competition makes, once the Competition is the one that is open
        var competition = (await ui.Get<IKrudListBehind<SetupCompetition>>().ReadMany()).Single();
        foreach (var node in ui.Get<IEnumerable<IKrudNodeSetter>>())
        {
            node.SetParent(competition);
        }

        await ui.Get<IKrudFormService<PhaseFormModel>>()
            .Create(
                new PhaseFormModel
                {
                    Loop = loops[0],
                    Recovery = 15,
                    Rest = 40,
                }
            );
        await ui.Get<IKrudFormService<PhaseFormModel>>().Create(new PhaseFormModel { Loop = loops[1], Recovery = 15 });
        await ui.Get<IKrudFormService<ParticipationFormModel>>()
            .Create(new ParticipationFormModel { Combination = combination, Category = ParticipationCategory.Senior });

        var stored = await setup.Open(id); // what the Api holds, read again
        var events = ui.Get<IConsoleEvents>();
        var issues = await events.Validate(id);
        await events.Start(id);
        await events.Load();

        Assert.NotNull(stored);
        Assert.Equal(2, stored.Model.Loops.Count);
        var stoppedCompetition = Assert.Single(stored.Model.Competitions);
        Assert.Equal("CEI 1*", stoppedCompetition.Name);
        Assert.Equal(2, stoppedCompetition.Phases.Count);
        Assert.Equal(7, Assert.Single(stoppedCompetition.Participations).Combination.Number);
        Assert.Equal("Ivan Petrov", Assert.Single(stored.Model.Combinations).Athlete.Name);
        Assert.Empty(issues); // the Setup that the Console composed is one that an Event can start from
        Assert.Equal(EventStage.Live, events.Events.Single(x => x.Id == id).Stage);
    }

    [Fact]
    public async Task The_form_of_the_Event_changes_its_name_and_leaves_what_the_Setup_is_made_of_where_it_was()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_shared.MongoConnectionString, tenant, consoleOperator.Id, "Before");
        await using var ui = ConsoleUi.Open(api, consoleOperator);
        var setup = ui.Get<IConsoleSetup>();
        var opened = (await setup.Open(id))!;

        opened.Model.Name = "After";
        opened.Model.Location = "Kaspichan";
        await ui.Get<IKrudFormService<ConfigureEventFormModel>>().Update(opened.Model);
        var stored = (await setup.Open(id))!;

        Assert.Equal("After", stored.Model.Name);
        Assert.Equal("Kaspichan", stored.Model.Location);
        Assert.Single(stored.Model.Competitions);
        Assert.Equal(3, stored.Model.Competitions.Single().Phases.Count);
        Assert.Single(stored.Model.Combinations);
        Assert.Equal(2, stored.Model.Loops.Count);
        Assert.Single(stored.Model.Officials);
    }

    [Fact]
    public async Task A_Setup_that_has_started_is_opened_to_be_read_and_the_Api_refuses_what_is_changed_in_it()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_shared.MongoConnectionString, tenant, consoleOperator.Id);
        await using var ui = ConsoleUi.Open(api, consoleOperator);
        await ui.Get<IConsoleEvents>().Load();
        await ui.Get<IConsoleEvents>().Start(id);

        var opened = await ui.Get<IConsoleSetup>().Open(id);
        Assert.NotNull(opened);
        var loops = opened.Model.Loops.Count;
        await ui.Get<IKrudFormService<LoopFormModel>>().Create(new LoopFormModel { Distance = 99 }); // the page offers no such change
        var after = await ui.Get<IConsoleSetup>().Open(id);

        Assert.False(opened.CanEdit); // the pages show it as it was configured, and offer no change
        Assert.Single(opened.Model.Competitions);
        Assert.NotNull(after);
        Assert.Equal(loops, after.Model.Loops.Count); // and the Api would not have taken it
    }

    [Fact]
    public async Task What_was_changed_a_moment_ago_is_saved_before_the_Setup_is_read_again_to_take_a_Participation_out_of_it()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_shared.MongoConnectionString, tenant, consoleOperator.Id, "Before");
        await using var ui = ConsoleUi.Open(api, consoleOperator);
        var setup = ui.Get<IConsoleSetup>();
        var opened = (await setup.Open(id))!;
        var competition = opened.Model.Competitions.Single();
        var number = competition.Participations.Single().Combination.Number;
        var order = new List<string>();
        ui.Requests.Before = async request =>
        {
            if (request.Method == HttpMethod.Patch)
            {
                await Task.Delay(600); // a change is saved a moment after the call that made it returns
                lock (order)
                {
                    order.Add("saved");
                }
            }
            else if (request.Method == HttpMethod.Get)
            {
                lock (order)
                {
                    order.Add("read");
                }
            }
        };
        opened.Model.Name = "After";

        await ui.Get<IKrudFormService<ConfigureEventFormModel>>().Update(opened.Model);
        await setup.RemoveParticipation(id, number, competition.Id);
        ui.Requests.Before = null;

        var stored = (await setup.Open(id))!;
        Assert.Equal("After", stored.Model.Name); // the Setup that the removal read was the one with the change in it
        Assert.Equal("saved", order.First()); // the change was saved before the Setup was read, not after
        Assert.Empty(stored.Model.Competitions.Single().Participations);
    }

    [Fact]
    public async Task A_Setup_that_the_Api_does_not_give_is_not_opened()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_shared.MongoConnectionString, tenant, consoleOperator.Id);
        await using var ui = ConsoleUi.Open(api, consoleOperator);
        ui.Requests.Answer = request =>
            request.Method == HttpMethod.Get
            && request.RequestUri!.AbsolutePath.EndsWith($"/{id}", StringComparison.Ordinal)
                ? new HttpResponseMessage(System.Net.HttpStatusCode.NotFound) // the list says it is there, and the read does not find it
                : null;

        var opened = await ui.Get<IConsoleSetup>().Open(id);

        Assert.Null(opened);
    }

    [Fact]
    public async Task A_Participation_is_taken_out_of_one_Competition_of_the_Setup_and_stays_in_the_others()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_shared.MongoConnectionString, tenant, consoleOperator.Id);
        await using var ui = ConsoleUi.Open(api, consoleOperator);
        var setup = ui.Get<IConsoleSetup>();
        var before = (await setup.Open(id))!.Model.Competitions.Single();
        var number = before.Participations.Single().Combination.Number;

        await setup.RemoveParticipation(id, number, before.Id);
        await setup.RemoveParticipation(id, number, before.Id); // again, which finds nothing to take out

        var after = (await setup.Open(id))!.Model.Competitions.Single();
        Assert.Empty(after.Participations);
        Assert.Equal(before.Phases.Count, after.Phases.Count);
        await Assert.ThrowsAsync<DomainException>(() => setup.RemoveParticipation(id, number, Guid.NewGuid()));
    }
}
