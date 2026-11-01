using System.Globalization;
using System.Net;
using System.Text;
using NoTiming.Ui.Features.Account;
using NoTiming.Ui.Features.Profile;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Features.Participants;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// What the person is at the Ui (#639, ADR-0010): the Athlete they bookmark as themselves, which is not a claim, and the Horses
/// they add, read and changed with the profile. The links are the host's (#638): the Ui changes one and reads the profile
/// again, so that what it shows is what the host keeps. Over the real Api in this process.
/// </summary>
public sealed class ProfileLinksTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public ProfileLinksTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_person_with_no_links_has_no_Athlete_and_no_Horses()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        using var ui = Open(api, person, out _);

        await ui.Profile.Load();

        Assert.Null(ui.Profile.Profile!.Athlete);
        Assert.Empty(ui.Profile.Profile.Horses);
    }

    [Fact]
    public async Task A_person_links_an_Athlete_as_themselves_and_linking_another_moves_the_link_and_it_is_taken_away()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("en");
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var first = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            "Първа Тестова",
            RegistrySeed.CountryOf("Bulgaria", "BG"),
            nameEnglish: "First Testova"
        );
        var second = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            "Втора Тестова",
            RegistrySeed.CountryOf("Bulgaria", "BG"),
            nameEnglish: "Second Testova"
        );
        using var ui = Open(api, person, out _);
        await ui.Profile.Load();
        var told = 0;
        ui.Profile.ObservableEvent.Subscribe(() => told++);

        await ui.Profile.LinkAthlete(first);
        var linkedFirst = ui.Profile.Profile!.Athlete;
        var toldAfterFirst = told;
        await ui.Profile.LinkAthlete(second);
        var linkedSecond = ui.Profile.Profile!.Athlete;
        using var another = Open(api, person, out _); // the host keeps it
        await another.Profile.Load();
        var kept = another.Profile.Profile!.Athlete;
        await ui.Profile.UnlinkAthlete();

        Assert.Equal((ParticipantKind.Athlete, first, "First Testova"), Of(linkedFirst));
        Assert.True(toldAfterFirst > 0); // the page follows
        Assert.Equal((ParticipantKind.Athlete, second, "Second Testova"), Of(linkedSecond));
        Assert.Equal((ParticipantKind.Athlete, second, "Second Testova"), Of(kept));
        Assert.Null(ui.Profile.Profile!.Athlete);
    }

    [Fact]
    public async Task A_person_adds_Horses_and_takes_them_away_and_the_host_keeps_them()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("en");
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var fire = await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, "Огън", nameEnglish: "Fire");
        var wind = await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, "Вятър", nameEnglish: "Wind");
        using var ui = Open(api, person, out _);
        await ui.Profile.Load();

        await ui.Profile.AddHorse(fire);
        await ui.Profile.AddHorse(wind);
        await ui.Profile.AddHorse(wind); // a Horse that is added is not added again
        var added = ui.Profile.Profile!.Horses.Select(Of).ToList();
        await ui.Profile.RemoveHorse(fire);
        using var another = Open(api, person, out _);
        await another.Profile.Load();

        Assert.Equal(
            new[] { (ParticipantKind.Horse, fire, "Fire"), (ParticipantKind.Horse, wind, "Wind") }.OrderBy(x =>
                x.Item2
            ),
            added.OrderBy(x => x.Item2)
        );
        Assert.Equal([(ParticipantKind.Horse, wind, "Wind")], another.Profile.Profile!.Horses.Select(Of));
        Assert.Equal([(ParticipantKind.Horse, wind, "Wind")], ui.Profile.Profile.Horses.Select(Of));
    }

    [Fact]
    public async Task The_names_of_the_links_are_told_in_the_language_the_app_is_in()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var fire = await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, "Огън", nameEnglish: "Fire");
        using var ui = Open(api, person, out _);
        await ui.Profile.Load();

        CultureInfo.CurrentUICulture = new CultureInfo("bg");
        await ui.Profile.AddHorse(fire);
        var inBulgarian = Assert.Single(ui.Profile.Profile!.Horses).DisplayName;
        CultureInfo.CurrentUICulture = new CultureInfo("en");
        await ui.Profile.AddHorse(fire);
        var inEnglish = Assert.Single(ui.Profile.Profile.Horses).DisplayName;

        Assert.Equal(("Огън", "Fire"), (inBulgarian, inEnglish));
    }

    [Fact]
    public async Task A_link_to_a_Participant_that_is_not_in_a_registry_is_refused_and_the_profile_stays_as_it_was()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        using var ui = Open(api, person, out _);
        await ui.Profile.Load();
        var before = ui.Profile.Profile;

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => ui.Profile.AddHorse(Guid.NewGuid()));

        Assert.Contains("not-found", refused.Message);
        Assert.Same(before, ui.Profile.Profile);
    }

    [Fact]
    public async Task A_Horse_the_host_will_not_add_says_why()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        using var ui = Open(api, person, out var asked);
        await ui.Profile.Load();
        asked.Answer = request =>
            request.Method == HttpMethod.Post
                ? new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
                {
                    Content = new StringContent(
                        """{"errors":[{"status":"422","code":"horse-limit-reached","title":"A person links at most 100 Horses."}]}""",
                        Encoding.UTF8,
                        "application/vnd.api+json"
                    ),
                }
                : null;

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => ui.Profile.AddHorse(Guid.NewGuid()));

        Assert.Contains("A person links at most 100 Horses.", refused.Message);
        Assert.Contains("horse-limit-reached", refused.Message);
    }

    [Fact]
    public async Task A_link_that_is_gone_when_it_is_taken_away_is_taken_away_all_the_same()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var athlete = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            "Първа Тестова",
            RegistrySeed.CountryOf("Bulgaria", "BG")
        );
        var horse = await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, "Огън");
        using var here = Open(api, person, out _);
        await here.Profile.Load();
        await here.Profile.LinkAthlete(athlete);
        await here.Profile.AddHorse(horse);
        using var there = Open(api, person, out _);
        await there.Profile.Load(); // the page over there shows both
        Assert.NotNull(there.Profile.Profile!.Athlete);
        Assert.Single(there.Profile.Profile.Horses);
        await here.Profile.UnlinkAthlete();
        await here.Profile.RemoveHorse(horse);

        await there.Profile.UnlinkAthlete(); // it still shows what is gone
        await there.Profile.RemoveHorse(horse);

        Assert.Null(there.Profile.Profile!.Athlete);
        Assert.Empty(there.Profile.Profile.Horses);
    }

    [Fact]
    public async Task Links_made_at_once_are_made_one_after_the_other_and_what_is_kept_is_the_latest_profile()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("en");
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var fire = await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, "Огън", nameEnglish: "Fire");
        var wind = await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, "Вятър", nameEnglish: "Wind");
        using var ui = Open(api, person, out var asked);
        await ui.Profile.Load();
        Task? second = null;
        var held = false;
        asked.After = async request =>
        {
            // the first profile that is read comes late, when the other link was made and its profile has been read
            if (
                !held
                && request.Method == HttpMethod.Get
                && request.RequestUri!.AbsolutePath.EndsWith("/me/profile", StringComparison.Ordinal)
            )
            {
                held = true;
                await Task.WhenAny(second!, Task.Delay(1500));
            }
        };

        var first = ui.Profile.AddHorse(fire);
        second = ui.Profile.AddHorse(wind);
        await Task.WhenAll(first, second);

        Assert.Equal(new[] { fire, wind }.Order(), ui.Profile.Profile!.Horses.Select(x => x.Id).Order());
    }

    [Fact]
    public async Task A_link_made_when_the_profile_cannot_be_read_again_says_so_and_the_profile_stays_as_it_was()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var fire = await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, "Огън", nameEnglish: "Fire");
        using var ui = Open(api, person, out var asked);
        await ui.Profile.Load();
        var before = ui.Profile.Profile;
        asked.Answer = request =>
            request.Method == HttpMethod.Get
            && request.RequestUri!.AbsolutePath.EndsWith("/me/profile", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(
                        """{"errors":[{"status":"500","code":"unexpected","title":"The profile could not be read."}]}""",
                        Encoding.UTF8,
                        "application/vnd.api+json"
                    ),
                }
                : null;

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => ui.Profile.AddHorse(fire));

        Assert.Contains("The profile could not be read.", refused.Message);
        Assert.Same(before, ui.Profile.Profile);
    }

    [Fact]
    public async Task A_visitor_links_nothing_and_asks_the_host_nothing()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var ui = Open(api, null, out var asked);
        await ui.Profile.Load();

        await Assert.ThrowsAsync<InvalidOperationException>(() => ui.Profile.AddHorse(Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ui.Profile.LinkAthlete(Guid.NewGuid()));

        Assert.Equal(["GET /api/me"], asked.Asked);
    }

    static (ParticipantKind, Guid, string) Of(ParticipantName? name)
    {
        return (name!.Kind, name.Id, name.DisplayName);
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
