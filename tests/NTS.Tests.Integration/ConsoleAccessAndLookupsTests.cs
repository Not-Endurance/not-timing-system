using Not.Domain.Exceptions;
using NTS.Contracts.Features.Console.Setup;
using NTS.Domain.Access;
using NTS.Domain.Enums;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Localization.NtsStrings;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The people a Main Operator links to the Officials and Operators of its Event, by the exact email, and what the forms of the
/// Setup pick from (#646, ADR-0010, ADR-0012). Over the real Api in this process and the container the app builds.
/// </summary>
public sealed class ConsoleAccessAndLookupsTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public ConsoleAccessAndLookupsTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_Main_Operator_links_an_account_by_its_exact_email_and_an_address_with_no_account_waits_as_an_invitation()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var known = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, name: "Georgi Ivanov");
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id, "Open");
        await using var ui = ConsoleUi.Open(api, mainOperator);
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
    public async Task The_Api_says_why_it_will_not_link_a_person_and_what_it_says_is_what_the_form_shows()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id, "Open");
        await using var mine = ConsoleUi.Open(api, mainOperator);
        await using var theirs = ConsoleUi.Open(api, root);

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
        Assert.NotEmpty(notTheirs.Message); // a Tenant Root who assigned the role away does not link people to the Event
        Assert.NotEmpty(noRole.Message);
        Assert.Empty(await mine.Get<IConsoleGrants>().List(id));
    }

    [Fact]
    public async Task An_Official_is_asked_for_a_role_before_the_Api_is_and_a_grant_is_removed_only_by_the_one_who_may()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var id = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id, "Open");
        await using var mine = ConsoleUi.Open(api, mainOperator);
        await using var theirs = ConsoleUi.Open(api, root);
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
    public async Task The_other_Tenants_are_asked_for_three_characters_a_quote_is_part_of_the_name_and_the_Tenants_own_rows_are_all_there_is_when_the_others_cannot_say()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var bulgaria = RegistrySeed.CountryOf("Bulgaria", "BG");
        var own = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            "Мария Тенант",
            bulgaria,
            "Maria Tenant"
        );
        var theirs = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            other,
            "Конан О'Брайън",
            bulgaria,
            "Conan O'Brien"
        );
        await using var ui = ConsoleUi.Open(api, mainOperator);
        var lookups = ui.Get<IConsoleLookups>();
        int Searches() => ui.Requests.Asked.Count(x => x.Contains("/all-tenants", StringComparison.Ordinal));

        await lookups.SearchAthletes("Co");
        var twoCharacters = Searches();
        await lookups.SearchAthletes("Con");
        var threeCharacters = Searches();
        var quoted = await lookups.SearchAthletes("O'Bri");
        ui.Requests.Answer = request =>
            request.RequestUri!.AbsolutePath.EndsWith("/all-tenants", StringComparison.Ordinal)
                ? new HttpResponseMessage((System.Net.HttpStatusCode)429)
                : null;
        var refused = await lookups.SearchAthletes("Maria");

        Assert.Equal(0, twoCharacters);
        Assert.Equal(1, threeCharacters);
        var found = Assert.Single(quoted);
        Assert.Equal(theirs, found.Id);
        Assert.Null(found.Club); // a row of another Tenant that has no Club has none
        Assert.Equal([own], refused.Select(x => x.Id)); // the Tenant's own rows are there when the others cannot be asked
    }

    [Fact]
    public async Task The_Tenants_own_rows_are_found_by_name_and_another_Tenants_rows_by_a_term_of_three_characters_or_more()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var bulgaria = RegistrySeed.CountryOf("Bulgaria", "BG");
        var own = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            "Мария Тенант",
            bulgaria,
            "Maria Tenant"
        );
        var theirs = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            other,
            "Мария Друга",
            bulgaria,
            "Maria Other"
        );
        await RegistrySeed.HorseAsync(_mongo.ConnectionString, other, "Далечен", "Faraway");
        await RegistrySeed.ClubAsync(_mongo.ConnectionString, other, "Далечен клуб");
        await using var ui = ConsoleUi.Open(api, mainOperator);
        var lookups = ui.Get<IConsoleLookups>();

        var byName = await lookups.SearchAthletes("Maria");
        var short1 = await lookups.SearchAthletes("Ma"); // too short to ask the other Tenants
        var englishName = await lookups.SearchAthletes("maria tenant");
        var horses = await lookups.SearchHorses("Farawa");
        var clubs = await lookups.SearchClubs("Далечен");

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
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        await TenantAsync(_mongo.ConnectionString, "Another Land", withCountry: true);
        await TenantAsync(_mongo.ConnectionString, "A Third Land", withCountry: true);
        await TenantAsync(_mongo.ConnectionString, "Yet Another Land", withCountry: true);
        var tenant = await TenantAsync(_mongo.ConnectionString, "Land of Test", withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var nobody = await SignedInAsync(api, client, _mongo.ConnectionString, null);
        await using var ui = ConsoleUi.Open(api, mainOperator);
        await using var none = ConsoleUi.Open(api, nobody);
        var lookups = ui.Get<IConsoleLookups>();

        var country = await lookups.TenantCountry();

        Assert.Null(await none.Get<IConsoleLookups>().TenantCountry()); // a person who acts in no Tenant has no country to start with
        Assert.NotNull(country);
        Assert.Equal("Land of Test", country.Name);
        Assert.Contains(country.Id, (await lookups.SearchCountries("land of")).Select(x => x.Id));
        Assert.Empty(await lookups.SearchCountries("no such country anywhere"));
    }
}
