using System.Net;
using System.Text.Json;
using NoTiming.Ui.Features.Account;
using NoTiming.Ui.Features.Profile;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Features.Profile;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// The profile of the person at the Ui (#645, ADR-0001, ADR-0012): what the host keeps of them, read and saved at
/// <c>/api/me/profile</c>. A visitor has none and needs none; a person who is signed in and has no country, first name or
/// surname is asked to complete it, which only sending a Snapshot requires; saving completes it and the account says so;
/// and a context that was loaded for a visitor loads again when the person signs in. Over the real Api in this process.
/// </summary>
public sealed class ProfileContextTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public ProfileContextTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_visitor_has_no_profile_and_is_asked_for_none()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var ui = Open(api, null, out var asked);

        await ui.Profile.Load();

        Assert.Null(ui.Profile.Profile);
        Assert.False(ui.Profile.RequiresProfileCompletion);
        Assert.Equal("", ui.Profile.WelcomeName);
        Assert.Equal(["GET /api/me"], asked.Asked); // only who is signed in: a visitor has no profile to read
    }

    [Fact]
    public async Task A_session_that_ended_while_the_profile_was_read_leaves_no_profile_and_is_not_read_again_and_again()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var home = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, home, name: "Ana Petrova");
        using var ui = Open(api, person, out var asked);
        asked.Answer = request =>
            request.RequestUri!.AbsolutePath.EndsWith("/me/profile", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : null;

        await ui.Profile.Load();
        await ui.Profile.Load();

        Assert.Null(ui.Profile.Profile);
        Assert.Single(asked.Asked, x => x.EndsWith("/me/profile", StringComparison.Ordinal)); // the host said so, and it is loaded
    }

    [Fact]
    public async Task A_person_who_is_signed_in_has_the_profile_the_host_keeps_and_is_welcomed_by_their_first_name()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var home = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, home, name: "Ana Petrova");
        using var ui = Open(api, person, out _);

        await ui.Profile.Load();

        var profile = Assert.IsType<AccountProfile>(ui.Profile.Profile);
        Assert.Equal("Ana", profile.GivenName);
        Assert.Equal("Petrova", profile.Surname);
        Assert.NotNull(profile.CountryRegion);
        Assert.True(profile.Complete);
        Assert.False(ui.Profile.RequiresProfileCompletion);
        Assert.Equal("Ana", ui.Profile.WelcomeName);
    }

    [Fact]
    public async Task A_person_without_a_country_is_asked_to_complete_the_profile_and_is_welcomed_by_what_there_is()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, null);
        using var ui = Open(api, person, out _);

        await ui.Profile.Load();

        Assert.False(ui.Profile.Profile!.Complete);
        Assert.True(ui.Profile.RequiresProfileCompletion);
        Assert.False(ui.Account.Current!.ProfileComplete);
        Assert.NotEqual("", ui.Profile.WelcomeName);
        Assert.True(ui.Profile.WelcomeName.Length <= 12);
    }

    [Fact]
    public async Task Saving_the_profile_completes_it_and_the_account_and_the_person_know_it()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var iso = CountrySeed.UniqueIsoCode();
        await CountrySeed.AddAsync(_mongo.ConnectionString, "Testland " + iso, iso, "T" + iso[..2]);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, null);
        using var ui = Open(api, person, out _);
        await ui.Profile.Load();
        var country = (await ui.Profile.SearchCountries(iso, CancellationToken.None)).Single();
        var model = ui.Profile.CreateFormModel();
        model.GivenName = "Vera";
        model.Surname = "Georgieva";
        model.Country = country;
        model.Club = "Sofia Riders";
        var told = 0;
        ui.Profile.ObservableEvent.Subscribe(() => told++);

        var saved = await ui.Profile.Save(model);

        Assert.True(saved.Complete);
        Assert.Equal("Vera", saved.GivenName);
        Assert.Equal(country.Id, saved.CountryId);
        Assert.Equal("Sofia Riders", saved.Club);
        Assert.False(ui.Profile.RequiresProfileCompletion);
        Assert.True(ui.Account.Current!.ProfileComplete); // the gate asks the account, which is asked again
        Assert.Equal("Vera", ui.Profile.WelcomeName);
        Assert.True(told > 0);
        var form = ui.Profile.CreateFormModel();
        Assert.Equal(
            ("Vera", "Georgieva", "Sofia Riders", country.Id),
            (form.GivenName, form.Surname, form.Club, form.Country!.Id)
        );
    }

    [Fact]
    public async Task A_profile_the_host_refuses_is_not_saved_and_says_why()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var iso = CountrySeed.UniqueIsoCode();
        await CountrySeed.AddAsync(_mongo.ConnectionString, "Testland " + iso, iso, "T" + iso[..2]);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, null, name: "Ana Petrova");
        using var ui = Open(api, person, out _);
        await ui.Profile.Load();
        var model = ui.Profile.CreateFormModel();
        model.Country = (await ui.Profile.SearchCountries(iso, CancellationToken.None)).Single();
        model.GivenName = new string('x', 101);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => ui.Profile.Save(model));

        Assert.Contains("invalid-name", refused.Message);
        Assert.Equal("Ana", ui.Profile.Profile!.GivenName);
    }

    [Fact]
    public async Task A_profile_that_lacks_what_is_required_is_not_sent_to_the_host_at_all()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var home = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, home, name: "Ana Petrova");
        using var ui = Open(api, person, out var asked);
        await ui.Profile.Load();
        var before = asked.Asked.Count;
        var model = ui.Profile.CreateFormModel();
        model.Surname = " ";

        await Assert.ThrowsAsync<InvalidOperationException>(() => ui.Profile.Save(model));

        Assert.Equal(before, asked.Asked.Count);
    }

    [Fact]
    public async Task The_countries_to_choose_from_are_found_by_name_ISO_code_and_NF_code_in_any_case()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var iso = CountrySeed.UniqueIsoCode();
        var nf = "N" + iso[..3];
        await CountrySeed.AddAsync(_mongo.ConnectionString, "Searchland " + iso, iso, nf);
        var last = CountrySeed.UniqueIsoCode();
        var first = CountrySeed.UniqueIsoCode();
        await CountrySeed.AddAsync(_mongo.ConnectionString, "Zzz " + last, last, "Z" + last[..2]); // stored before the one it is to follow
        await CountrySeed.AddAsync(_mongo.ConnectionString, "!!! " + first, first, "A" + first[..2]);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, null);
        using var ui = Open(api, person, out _);
        await ui.Profile.Load();

        var byName = await ui.Profile.SearchCountries("searchland " + iso.ToLowerInvariant(), CancellationToken.None);
        var byIso = await ui.Profile.SearchCountries(iso.ToLowerInvariant(), CancellationToken.None);
        var byNf = await ui.Profile.SearchCountries(nf.ToLowerInvariant(), CancellationToken.None);
        var none = await ui.Profile.SearchCountries("no such country " + Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("Searchland " + iso, Assert.Single(byName).Name);
        Assert.Equal(iso, Assert.Single(byIso).IsoCode);
        Assert.Equal(nf, Assert.Single(byNf).NfCode);
        Assert.Empty(none);
        var all = (await ui.Profile.SearchCountries("", CancellationToken.None)).Select(x => x.IsoCode).ToList();
        Assert.Contains(iso, all);
        Assert.True(all.IndexOf(first) < all.IndexOf(last)); // by name, whatever the order they are kept in
    }

    [Fact]
    public async Task What_the_person_typed_is_saved_without_the_spaces_around_it()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var iso = CountrySeed.UniqueIsoCode();
        await CountrySeed.AddAsync(_mongo.ConnectionString, "Testland " + iso, iso, "T" + iso[..2]);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, null);
        using var ui = Open(api, person, out var asked);
        await ui.Profile.Load();
        string? sent = null;
        asked.Before = async request =>
        {
            if (
                request.Method == HttpMethod.Patch
                && request.RequestUri!.AbsolutePath.EndsWith("/me/profile", StringComparison.Ordinal)
            )
            {
                sent = await request.Content!.ReadAsStringAsync();
            }
        };
        var model = ui.Profile.CreateFormModel();
        model.GivenName = "  Vera ";
        model.MiddleName = " K. ";
        model.Surname = " Georgieva  ";
        model.Country = (await ui.Profile.SearchCountries(iso, CancellationToken.None)).Single();
        model.Club = "  Sofia Riders";
        model.FeiId = " 10012345 ";

        await ui.Profile.Save(model);

        using var document = JsonDocument.Parse(sent!);
        var attributes = document.RootElement.GetProperty("data").GetProperty("attributes");
        Assert.Equal(
            ["Vera", "K.", "Georgieva", "Sofia Riders", "10012345"],
            new[] { "givenName", "middleName", "surname", "club", "feiId" }.Select(x =>
                attributes.GetProperty(x).GetString()
            )
        );
    }

    [Fact]
    public async Task A_context_loaded_for_a_visitor_loads_again_when_the_person_signs_in_and_forgets_them_when_they_sign_out()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();
        var home = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, home, name: "Ana Petrova");
        var cookie = new JsonApiClients.Cookie(null);
        var json = JsonApiClients.WithCookie(api, cookie, out _);
        using var account = new AccountSession(json);
        using var profile = new WitnessProfileContext(account, new CountryApiRepository(json), json);
        await profile.Load();
        Assert.Null(profile.Profile); // the app booted as a visitor

        cookie.Value = $"{ApiSessions.COOKIE_NAME}={person.Page.Cookie(ApiSessions.COOKIE_NAME)}";
        await account.Refresh(); // the person signs in on the server page and the app is told

        await Eventually(() => profile.Profile != null);
        Assert.Equal("Ana", profile.Profile!.GivenName);

        await account.SignOut();

        await Eventually(() => profile.Profile == null);
        Assert.Equal("", profile.WelcomeName);
    }

    static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!condition() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(25);
        }

        Assert.True(condition(), "The context did not follow the account.");
    }

    static UiOfAPerson Open(ApiFactory api, TenancySeed.Person? person, out JsonApiClients.Requests asked)
    {
        var json = JsonApiClients.Of(api, person, out asked);
        var account = new AccountSession(json);
        return new UiOfAPerson(account, new WitnessProfileContext(account, new CountryApiRepository(json), json));
    }

    sealed class UiOfAPerson : IDisposable
    {
        public UiOfAPerson(AccountSession account, WitnessProfileContext profile)
        {
            Account = account;
            Profile = profile;
        }

        public AccountSession Account { get; }
        public WitnessProfileContext Profile { get; }

        public void Dispose()
        {
            Profile.Dispose();
            Account.Dispose();
        }
    }
}
