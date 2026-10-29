using MongoDB.Bson;
using Not.Application.HTTP;
using Not.Domain.Exceptions;
using NoTiming.Ui.Features.Console;
using NTS.Contracts.Features.Console.Setup;
using NTS.Domain.Access;
using NTS.Domain.Enums;
using NTS.Tests.Integration.Harness;
using static NoTiming.Ui.Features.Console.ApiAnswers;
using static NTS.Localization.NtsStrings;
using static NTS.Tests.Integration.Harness.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The people a Console Operator links to the Officials and Operators of its Event, by the exact email, and what the forms of the
/// Setup pick from (#646, ADR-0010, ADR-0012). Over the real Api in this process and the container the app builds.
/// </summary>
public sealed class ConsoleAccessAndLookupsTests : IClassFixture<SharedApiFixture>
{
    readonly SharedApiFixture _shared;

    public ConsoleAccessAndLookupsTests(SharedApiFixture shared)
    {
        _shared = shared;
    }

    [Fact]
    public async Task A_Console_Operator_links_an_account_by_its_exact_email_and_an_address_with_no_account_waits_as_an_invitation()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var known = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant, name: "Georgi Ivanov");
        var id = await EventSeed.SetupAsync(_shared.MongoConnectionString, tenant, consoleOperator.Id, "Open");
        await using var ui = ConsoleUi.Open(api, consoleOperator);
        var grants = ui.Get<IConsoleGrants>();

        var official = await grants.Link(
            id,
            GrantKind.Official,
            OfficialRole.Steward,
            $"  {known.Email.ToUpperInvariant()} "
        );
        var invited = await grants.Link(id, GrantKind.Operator, null, "nobody.yet@example.test");
        var again = await grants.Link(id, GrantKind.Operator, null, "Nobody.Yet@example.test");
        var listed = await grants.List(id);

        Assert.False(official.IsPending); // an address that has an account gives it the access at once
        Assert.Equal("Georgi Ivanov", official.DisplayName);
        Assert.Equal(OfficialRole.Steward, official.Role);
        Assert.True(invited.IsPending); // and one that has none waits for the person to register
        Assert.Null(invited.DisplayName);
        Assert.Null(invited.Role);
        Assert.Equal(invited.Id, again.Id); // the same person in the same kind is one grant, whatever the case of the address
        Assert.DoesNotContain("nobody.yet@example.test", invited.Email); // an address is never given back whole
        Assert.Equal(2, listed.Count);
        Assert.Contains(listed, x => x.Id == official.Id && x.Kind == GrantKind.Official);

        await grants.Remove(invited.Id);

        Assert.Equal([official.Id], (await grants.List(id)).Select(x => x.Id));
    }

    [Fact]
    public async Task A_list_that_the_Api_cuts_into_pages_is_read_page_after_page_to_its_end_and_the_grants_ask_for_the_biggest_page()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var id = await EventSeed.SetupAsync(_shared.MongoConnectionString, tenant, consoleOperator.Id, "Open");
        await using var ui = ConsoleUi.Open(api, consoleOperator);
        var grants = ui.Get<IConsoleGrants>();
        var marker = Guid.NewGuid().ToString("N");
        var linked = new List<Guid>();
        foreach (var name in new[] { "aaa", "bbb", "ccc" })
        {
            linked.Add((await grants.Link(id, GrantKind.Operator, null, $"{name}.{marker}@example.test")).Id);
        }

        ui.Requests.Asked.Clear();
        var pages = await AcceptedPages(
            ui.Get<JsonApiClient>(),
            $"event-grants?filter={Uri.EscapeDataString($"eventId eq {id}")}",
            x => Guid.Parse(x.GetProperty("id").GetString()!),
            pageSize: 2
        );
        var asked = ui.Requests.Asked.Select(Unescaped).ToList();
        ui.Requests.Asked.Clear();
        var listed = await grants.List(id);

        Assert.Equal(linked, pages); // the three, in the order of their emails, from two pages
        Assert.Equal(2, asked.Count);
        Assert.Contains("page[number]=1", asked[0]);
        Assert.Contains("page[number]=2", asked[1]);
        Assert.All(asked, x => Assert.Contains("page[size]=2", x));
        Assert.Equal(linked, listed.Select(x => x.Id));
        var listing = Assert.Single(ui.Requests.Asked); // and the page the grants ask for holds them all
        Assert.Contains("page[size]=500", Unescaped(listing));
    }

    [Fact]
    public async Task The_Api_says_why_it_will_not_link_a_person_and_what_it_says_is_what_the_form_shows()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var member = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var id = await EventSeed.SetupAsync(_shared.MongoConnectionString, tenant, consoleOperator.Id, "Open");
        await using var mine = ConsoleUi.Open(api, consoleOperator);
        await using var theirs = ConsoleUi.Open(api, member);

        var notAnAddress = await Assert.ThrowsAsync<DomainException>(
            () => mine.Get<IConsoleGrants>().Link(id, GrantKind.Operator, null, "not an address")
        );
        var notTheirs = await Assert.ThrowsAsync<DomainException>(
            () => theirs.Get<IConsoleGrants>().Link(id, GrantKind.Operator, null, "someone@example.test")
        );
        var noRole = await Assert.ThrowsAsync<DomainException>(
            () => mine.Get<IConsoleGrants>().Link(id, GrantKind.Official, null, "someone@example.test")
        );

        Assert.Contains("email", notAnAddress.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(notTheirs.Message); // an account that holds nothing does not link people to the Event
        Assert.NotEmpty(noRole.Message);
        Assert.Empty(await mine.Get<IConsoleGrants>().List(id));
    }

    [Fact]
    public async Task An_Official_is_asked_for_a_role_before_the_Api_is_and_a_grant_is_removed_only_by_the_one_who_may()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var member = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var id = await EventSeed.SetupAsync(_shared.MongoConnectionString, tenant, consoleOperator.Id, "Open");
        await using var mine = ConsoleUi.Open(api, consoleOperator);
        await using var theirs = ConsoleUi.Open(api, member);
        var grant = await mine.Get<IConsoleGrants>().Link(id, GrantKind.Operator, null, "someone@example.test");
        var asked = mine.Requests.Asked.Count;

        var noRole = await Assert.ThrowsAsync<DomainException>(
            () => mine.Get<IConsoleGrants>().Link(id, GrantKind.Official, null, "other@example.test")
        );
        var notTheirs = await Assert.ThrowsAsync<DomainException>(() => theirs.Get<IConsoleGrants>().Remove(grant.Id));

        Assert.Equal(Select_a_role_string, noRole.Message);
        Assert.Equal(asked, mine.Requests.Asked.Count); // the Api was not asked
        Assert.NotEmpty(notTheirs.Message);
        Assert.Equal([grant.Id], (await mine.Get<IConsoleGrants>().List(id)).Select(x => x.Id)); // and it is still there
    }

    [Fact]
    public async Task The_other_Tenants_are_asked_for_two_characters_a_quote_is_part_of_the_name_and_the_Tenants_own_rows_are_all_there_is_when_the_others_cannot_say()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var other = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var bulgaria = RegistrySeed.CountryOf("Bulgaria", "BG");
        var own = await RegistrySeed.AthleteAsync(
            _shared.MongoConnectionString,
            tenant,
            "Мария Тенант",
            bulgaria,
            "Maria Tenant"
        );
        var theirs = await RegistrySeed.AthleteAsync(
            _shared.MongoConnectionString,
            other,
            "Конан О'Брайън",
            bulgaria,
            "Conan O'Brien"
        );
        await using var ui = ConsoleUi.Open(api, consoleOperator);
        var lookups = ui.Get<IConsoleLookups>();
        int Searches() => ui.Requests.Asked.Count(x => x.Contains("/all-tenants", StringComparison.Ordinal));

        await lookups.SearchAthletes("C");
        var oneCharacter = Searches();
        await lookups.SearchAthletes("Co");
        var twoCharacters = Searches();
        var quoted = await lookups.SearchAthletes("O'Bri");
        ui.Requests.Answer = request =>
            request.RequestUri!.AbsolutePath.EndsWith("/all-tenants", StringComparison.Ordinal)
                ? new HttpResponseMessage((System.Net.HttpStatusCode)429)
                : null;
        var refused = await lookups.SearchAthletes("Maria");

        Assert.Equal(0, oneCharacter);
        Assert.Equal(1, twoCharacters);
        var found = Assert.Single(quoted);
        Assert.Equal(theirs, found.Id);
        Assert.Null(found.Club); // a row of another Tenant that has no Club has none
        Assert.Equal([own], refused.Select(x => x.Id)); // the Tenant's own rows are there when the others cannot be asked
    }

    [Fact]
    public async Task The_FEI_ID_of_a_row_of_another_Tenant_reaches_the_Setup_of_a_Tenant_Root_and_no_other_account()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var other = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant, TenantRootOf(tenant));
        var member = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var (latin, cyrillic) = HistorySeed.NameMarker(); // the registries are shared by the tests of this class: the name is this test's own
        var theirs = await RegistrySeed.AthleteAsync(
            _shared.MongoConnectionString,
            other,
            $"Мария Друга {cyrillic}",
            RegistrySeed.CountryOf("Bulgaria", "BG"),
            $"Maria Other {latin}",
            feiId: "10009999"
        );
        await using var rootUi = ConsoleUi.Open(api, root);
        await using var memberUi = ConsoleUi.Open(api, member);

        var forTheRoot = await rootUi.Get<IConsoleLookups>().SearchAthletes(latin);
        var forTheMember = await memberUi.Get<IConsoleLookups>().SearchAthletes(latin);

        Assert.Equal(("10009999", theirs), (Assert.Single(forTheRoot).FeiId, forTheRoot[0].Id)); // what a Combination and the FEI export need
        Assert.Equal(theirs, Assert.Single(forTheMember).Id);
        Assert.Null(forTheMember[0].FeiId); // an account that sets nothing up reads the name of a rider, and not the FEI ID
    }

    [Fact]
    public async Task Other_Tenants_are_asked_for_a_term_with_a_word_of_two_characters_and_for_the_first_six_words_of_it()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var other = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var (latin, cyrillic) = HistorySeed.NameMarker(); // the registries are shared by the tests of this class: the name is this test's own
        var theirs = await RegistrySeed.AthleteAsync(
            _shared.MongoConnectionString,
            other,
            $"Конан {cyrillic}",
            RegistrySeed.CountryOf("Bulgaria", "BG"),
            $"Conan {latin}"
        );
        await using var ui = ConsoleUi.Open(api, consoleOperator);
        var lookups = ui.Get<IConsoleLookups>();
        int Searches() => ui.Requests.Asked.Count(x => x.Contains("/all-tenants", StringComparison.Ordinal));

        await lookups.SearchAthletes("C B"); // three characters, but no word of two: the Api would refuse it
        var shortWords = Searches();
        var sevenWords = await lookups.SearchAthletes($"Conan {latin} c o n a r"); // the Api takes six words and the seventh is left out

        Assert.Equal(0, shortWords);
        Assert.Equal(theirs, Assert.Single(sevenWords).Id);
    }

    [Fact]
    public async Task The_Tenants_own_rows_are_found_by_name_and_another_Tenants_rows_by_a_term_with_a_word_of_two_characters_or_more()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var other = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var bulgaria = RegistrySeed.CountryOf("Bulgaria", "BG");
        var (latin, cyrillic) = HistorySeed.NameMarker(); // the registries are shared by the tests of this class: the names are this test's own
        var own = await RegistrySeed.AthleteAsync(
            _shared.MongoConnectionString,
            tenant,
            $"Мария Тенант {cyrillic}",
            bulgaria,
            $"Maria Tenant {latin}"
        );
        var theirs = await RegistrySeed.AthleteAsync(
            _shared.MongoConnectionString,
            other,
            $"Мария Друга {cyrillic}",
            bulgaria,
            $"Maria Other {latin}"
        );
        await RegistrySeed.HorseAsync(_shared.MongoConnectionString, other, $"Далечен {cyrillic}", $"Faraway {latin}");
        await RegistrySeed.ClubAsync(_shared.MongoConnectionString, other, $"Далечен клуб {cyrillic}");
        await using var ui = ConsoleUi.Open(api, consoleOperator);
        var lookups = ui.Get<IConsoleLookups>();

        var byName = await lookups.SearchAthletes(latin);
        var short1 = await lookups.SearchAthletes("M"); // too short to ask the other Tenants
        var englishName = await lookups.SearchAthletes($"maria tenant {latin}");
        var horses = await lookups.SearchHorses($"Farawa {latin}");
        var clubs = await lookups.SearchClubs($"Далечен {cyrillic}");

        Assert.Equal([own, theirs], byName.Select(x => x.Id)); // the Tenant's own first, then the others'
        Assert.Equal([own], short1.Select(x => x.Id));
        Assert.Equal([own], englishName.Select(x => x.Id));
        Assert.Single(horses);
        Assert.Single(clubs);
        Assert.Equal([own], (await lookups.SearchAthletes(string.Empty)).Select(x => x.Id)); // no term lists the Tenant's own rows and asks no other Tenant
    }

    [Fact]
    public async Task The_country_of_the_Tenant_is_the_country_that_a_new_row_starts_with_and_countries_are_found_by_name()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        await TenantAsync(_shared.MongoConnectionString, "Another Land", withCountry: true);
        await TenantAsync(_shared.MongoConnectionString, "A Third Land", withCountry: true);
        await TenantAsync(_shared.MongoConnectionString, "Yet Another Land", withCountry: true);
        await CountrySeed
            .Countries(_shared.MongoConnectionString)
            .InsertOneAsync(
                new BsonDocument
                {
                    {
                        "_id",
                        new BsonBinaryData(
                            Guid.Parse("00" + Guid.NewGuid().ToString()[2..]),
                            GuidRepresentation.Standard
                        )
                    },
                    { "TenantId", "nts" },
                    { "Name", "Aardvark Land" },
                    { "IsoCode", CountrySeed.UniqueIsoCode() },
                }
            ); // the first of the countries by its id, which is not the country of the Tenant
        var tenant = await TenantAsync(_shared.MongoConnectionString, "Land of Test", withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var nobody = await SignedInAsync(api, client, _shared.MongoConnectionString, null);
        await using var ui = ConsoleUi.Open(api, consoleOperator);
        await using var none = ConsoleUi.Open(api, nobody);
        var lookups = ui.Get<IConsoleLookups>();

        var country = await lookups.TenantCountry();

        Assert.Null(await none.Get<IConsoleLookups>().TenantCountry()); // a person who acts in no Tenant has no country to start with
        Assert.NotNull(country);
        Assert.Equal("Land of Test", country.Name);
        Assert.Contains(country.Id, (await lookups.SearchCountries("land of")).Select(x => x.Id));
        Assert.Empty(await lookups.SearchCountries("no such country anywhere"));
    }

    static string Unescaped(string request)
    {
        return request.Replace("%5B", "[").Replace("%5D", "]");
    }
}
