using System.Globalization;
using System.Net;
using Microsoft.Extensions.Time.Testing;
using NoTiming.Ui.Features.Participants;
using NoTiming.Ui.Storage.Core.Repositories;
using NTS.Contracts.Features.Participants;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Enums;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// The Participants at the Ui (#639, ADR-0010): an Athlete or a Horse is found by the words of its name, in the script it is
/// registered in or in English, a page of ten at a time, and told in the language the app is in, and the history of one is read
/// a page at a time. Over the real Api in this process, as the Ui reaches it.
/// </summary>
public sealed class ParticipantServiceTests : IClassFixture<MongoFixture>
{
    // Every test has Participants of its own: the database is shared by all of them, and a Participant is found by its id.
    readonly Participant _ana = new(Guid.NewGuid(), "Мариела Тестова", "Mariela Testova");
    readonly Participant _fire = new(Guid.NewGuid(), "Огън", "Fire");
    readonly Participant _wind = new(Guid.NewGuid(), "Вятър", "Wind");
    readonly MongoFixture _mongo;

    public ParticipantServiceTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_text_finds_the_Athletes_or_the_Horses_by_their_names_in_either_script_and_each_is_told_with_its_kind()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("en");
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var (latin, cyrillic) = HistorySeed.NameMarker();
        var athlete = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            $"Иван {cyrillic}ов",
            RegistrySeed.CountryOf("Bulgaria", "BG"),
            nameEnglish: $"Ivan {latin}ov"
        );
        var horse = await RegistrySeed.HorseAsync(
            _mongo.ConnectionString,
            tenant,
            $"Звезда {cyrillic}ов",
            nameEnglish: $"Star {latin}ov"
        );
        var service = Open(api, person, out _);

        var athletes = await service.Search(ParticipantKind.Athlete, $"{latin}ov", 1, CancellationToken.None);
        var horses = await service.Search(ParticipantKind.Horse, $"{latin}ov", 1, CancellationToken.None);
        var byCyrillic = await service.Search(ParticipantKind.Athlete, $"{cyrillic}ОВ иван", 1, CancellationToken.None);

        Assert.Equal([(ParticipantKind.Athlete, athlete, $"Ivan {latin}ov")], athletes.Items.Select(Of));
        Assert.Equal([(ParticipantKind.Horse, horse, $"Star {latin}ov")], horses.Items.Select(Of));
        Assert.False(athletes.HasMore);
        Assert.Equal([athlete], byCyrillic.Items.Select(x => x.Id)); // the name in the script it is registered in, in any case and in any order
    }

    [Fact]
    public async Task A_search_is_a_page_of_ten_and_the_rest_is_on_the_next_page()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("en");
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var (latin, _) = HistorySeed.NameMarker();
        for (var i = 0; i < 12; i++)
        {
            await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, $"Horse {latin} {i:00}");
        }

        var service = Open(api, person, out var asked);

        var first = await service.Search(ParticipantKind.Horse, latin, 1, CancellationToken.None);
        var second = await service.Search(ParticipantKind.Horse, latin, 2, CancellationToken.None);

        Assert.Equal(
            Enumerable.Range(0, 10).Select(i => $"Horse {latin} {i:00}"),
            first.Items.Select(x => x.DisplayName)
        );
        Assert.True(first.HasMore);
        Assert.Equal([$"Horse {latin} 10", $"Horse {latin} 11"], second.Items.Select(x => x.DisplayName));
        Assert.False(second.HasMore);
        Assert.All(
            asked.Asked,
            x =>
            {
                Assert.StartsWith("GET /api/horses/all-tenants?filter=", x, StringComparison.Ordinal);
                Assert.Contains("&sort=name&page%5Bsize%5D=10&page%5Bnumber%5D=", x, StringComparison.Ordinal);
            }
        );
    }

    [Fact]
    public async Task A_text_of_more_than_six_words_is_asked_for_with_the_first_six()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var service = Open(api, person, out var asked);

        var found = await service.Search(
            ParticipantKind.Athlete,
            " one  two three four five six seven eight ",
            1,
            CancellationToken.None
        );

        Assert.Empty(found.Items);
        var request = Assert.Single(asked.Asked);
        Assert.Contains(Uri.EscapeDataString("contains(name,'one two three four five six')"), request);
    }

    [Fact]
    public async Task The_names_are_told_in_the_language_the_app_is_in()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var (latin, cyrillic) = HistorySeed.NameMarker();
        await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            $"Иван {cyrillic}ов",
            RegistrySeed.CountryOf("Bulgaria", "BG"),
            nameEnglish: $"Ivan {latin}ov"
        );
        var service = Open(api, person, out _);

        CultureInfo.CurrentUICulture = new CultureInfo("bg");
        var inBulgarian = await service.Search(ParticipantKind.Athlete, $"{latin}ov", 1, CancellationToken.None);
        CultureInfo.CurrentUICulture = new CultureInfo("en-GB");
        var inEnglish = await service.Search(ParticipantKind.Athlete, $"{latin}ov", 1, CancellationToken.None);
        CultureInfo.CurrentUICulture = new CultureInfo("de");
        var inGerman = await service.Search(ParticipantKind.Athlete, $"{latin}ov", 1, CancellationToken.None);

        Assert.Equal($"Иван {cyrillic}ов", Assert.Single(inBulgarian.Items).DisplayName);
        Assert.Equal($"Ivan {latin}ov", Assert.Single(inEnglish.Items).DisplayName);
        Assert.Equal($"Иван {cyrillic}ов", Assert.Single(inGerman.Items).DisplayName); // as it was registered, where the language is not English
    }

    [Theory]
    [InlineData("")]
    [InlineData("i")]
    [InlineData("  i  ")]
    [InlineData("i a")]
    [InlineData("a b c d")]
    [InlineData("a b c d e f ab")] // the long word is the seventh, and the Api is asked for the first six
    public async Task A_text_with_no_word_of_two_characters_finds_none_and_asks_the_Api_nothing(string text)
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var service = Open(api, person, out var asked);

        var found = await service.Search(ParticipantKind.Horse, text, 1, CancellationToken.None);

        Assert.Empty(found.Items);
        Assert.False(found.HasMore);
        Assert.Empty(asked.Asked); // a search counts against the budget of the account
    }

    [Fact]
    public async Task A_search_the_Api_refuses_says_so()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var service = Open(api, person, out var asked);
        asked.Answer = _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.Search(ParticipantKind.Athlete, "ivan", 1, CancellationToken.None)
        );

        Assert.Equal("The server answered 429.", refused.Message); // what the Api answered, and not the failure of reading an answer that is not there
    }

    [Fact]
    public async Task What_was_answered_is_not_asked_again_and_a_text_that_only_grows_from_a_complete_answer_is_answered_without_asking()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("en");
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(
            api,
            client,
            _mongo.ConnectionString,
            tenant,
            TenancySeed.TenantRootOf(tenant)
        ); // one of the people who set an Event up, who read the FEI ID of a row
        var (latin, cyrillic) = HistorySeed.NameMarker();
        var bulgaria = RegistrySeed.CountryOf("Bulgaria", "BG");
        var ivan = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            $"Иван {cyrillic}ов",
            bulgaria,
            nameEnglish: $"Ivan {latin}ov",
            feiId: "10007777"
        );
        var petar = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            $"Петър {cyrillic}ев",
            bulgaria,
            nameEnglish: $"Petar {latin}ev",
            feiId: "10008888"
        );
        var service = Open(api, person, out var asked);
        var athlete = ParticipantKind.Athlete;

        var both = await service.Search(athlete, latin, 1, CancellationToken.None); // asked
        var again = await service.Search(athlete, latin.ToUpperInvariant(), 1, CancellationToken.None); // the same word in capitals
        var grown = await service.Search(athlete, $"{latin}ov", 1, CancellationToken.None); // the word gets longer
        var added = await service.Search(athlete, $"ivan {latin}ov", 1, CancellationToken.None); // a word comes with it
        var byFeiId = await service.Search(athlete, $"{latin} 8888", 1, CancellationToken.None); // the FEI ID is matched as the Api matches it
        var requests = asked.Asked.Count;
        var another = await service.Search(athlete, $"{cyrillic}ев", 1, CancellationToken.None); // not a text that grows from the first: asked

        Assert.Equal([petar], byFeiId.Items.Select(x => x.Id));
        Assert.Equal(1, requests);
        Assert.Equal(2, asked.Asked.Count);
        Assert.Equal(new[] { ivan, petar }.Order(), both.Items.Select(x => x.Id).Order());
        Assert.Equal(both.Items.Select(x => x.Id), again.Items.Select(x => x.Id));
        Assert.Equal([ivan], grown.Items.Select(x => x.Id));
        Assert.Equal([ivan], added.Items.Select(x => x.Id));
        Assert.Equal([petar], another.Items.Select(x => x.Id));
        Assert.False(grown.HasMore);
        var fresh = Open(api, person, out _); // and what it tells is what the Api would have told
        Assert.Equal(
            (await fresh.Search(athlete, $"ivan {latin}ov", 1, CancellationToken.None)).Items.Select(Of),
            added.Items.Select(Of)
        );
    }

    [Fact]
    public async Task An_account_that_sets_nothing_up_finds_a_Participant_by_its_names_and_not_by_its_FEI_ID_whether_the_Api_or_the_memory_answers()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("en");
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var (latin, cyrillic) = HistorySeed.NameMarker();
        var ivan = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            $"Иван {cyrillic}ов",
            RegistrySeed.CountryOf("Bulgaria", "BG"),
            nameEnglish: $"Ivan {latin}ov",
            feiId: "10007777"
        );
        var service = Open(api, person, out _);
        var fresh = Open(api, person, out _);

        var byName = await service.Search(ParticipantKind.Athlete, latin, 1, CancellationToken.None);
        var remembered = await service.Search(ParticipantKind.Athlete, $"{latin} 7777", 1, CancellationToken.None);
        var asked = await fresh.Search(ParticipantKind.Athlete, $"{latin} 7777", 1, CancellationToken.None);

        Assert.Equal([ivan], byName.Items.Select(x => x.Id));
        Assert.Empty(remembered.Items); // the row does not tell its FEI ID to this account, so the memory cannot match it
        Assert.Empty(asked.Items); // and the Api does not match it either
    }

    [Fact]
    public async Task A_page_is_remembered_by_its_number_and_a_page_that_is_not_complete_is_not_narrowed()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("en");
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var (latin, _) = HistorySeed.NameMarker();
        for (var i = 0; i < 12; i++)
        {
            await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, $"Horse {latin} {i:00}");
        }

        var service = Open(api, person, out var asked);
        var horse = ParticipantKind.Horse;

        await service.Search(horse, latin, 1, CancellationToken.None);
        await service.Search(horse, latin, 2, CancellationToken.None);
        var firstAgain = await service.Search(horse, latin, 1, CancellationToken.None);
        var secondAgain = await service.Search(horse, latin, 2, CancellationToken.None);
        var requests = asked.Asked.Count;
        var longer = await service.Search(horse, $"{latin} 05", 1, CancellationToken.None); // the first answer had a next page, so it does not hold every row

        Assert.Equal(2, requests);
        Assert.Equal(3, asked.Asked.Count);
        Assert.Equal(10, firstAgain.Items.Count);
        Assert.True(firstAgain.HasMore);
        Assert.Equal(2, secondAgain.Items.Count);
        Assert.Equal([$"Horse {latin} 05"], longer.Items.Select(x => x.DisplayName));
    }

    [Fact]
    public async Task A_remembered_answer_is_told_in_the_language_the_app_is_in_when_it_is_asked_for_and_not_in_the_one_it_was_made_in()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var (latin, cyrillic) = HistorySeed.NameMarker();
        await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            $"Иван {cyrillic}ов",
            RegistrySeed.CountryOf("Bulgaria", "BG"),
            nameEnglish: $"Ivan {latin}ov"
        );
        var service = Open(api, person, out var asked);

        CultureInfo.CurrentUICulture = new CultureInfo("en");
        var inEnglish = await service.Search(ParticipantKind.Athlete, $"{latin}ov", 1, CancellationToken.None);
        CultureInfo.CurrentUICulture = new CultureInfo("bg");
        var inBulgarian = await service.Search(ParticipantKind.Athlete, $"{latin}ov", 1, CancellationToken.None);
        var again = await service.Search(ParticipantKind.Athlete, $"{latin}ov", 1, CancellationToken.None);

        Assert.Single(asked.Asked);
        Assert.Equal($"Ivan {latin}ov", Assert.Single(inEnglish.Items).DisplayName);
        Assert.Equal($"Иван {cyrillic}ов", Assert.Single(inBulgarian.Items).DisplayName);
        Assert.Equal($"Иван {cyrillic}ов", Assert.Single(again.Items).DisplayName);
    }

    [Fact]
    public async Task A_search_the_Api_refused_is_not_remembered_and_an_answer_is_forgotten_after_a_minute()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2030, 5, 21, 8, 0, 0, TimeSpan.Zero));
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var service = Open(api, person, out var asked, time);
        var (latin, _) = HistorySeed.NameMarker();
        asked.Answer = _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.Search(ParticipantKind.Athlete, latin, 1, CancellationToken.None)
        );
        asked.Answer = null;
        await service.Search(ParticipantKind.Athlete, latin, 1, CancellationToken.None); // asked again, and answered
        await service.Search(ParticipantKind.Athlete, latin, 1, CancellationToken.None); // remembered
        var before = asked.Asked.Count;
        time.Advance(TimeSpan.FromSeconds(59));
        await service.Search(ParticipantKind.Athlete, latin, 1, CancellationToken.None); // still remembered
        var kept = asked.Asked.Count;
        time.Advance(TimeSpan.FromSeconds(2));
        await service.Search(ParticipantKind.Athlete, latin, 1, CancellationToken.None); // a registry may have changed since

        Assert.Equal(2, before);
        Assert.Equal(2, kept);
        Assert.Equal(3, asked.Asked.Count);
    }

    [Fact]
    public async Task The_history_of_an_Athlete_is_read_a_page_at_a_time_newest_first_with_the_other_half_the_placing_and_the_other_Rankings()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("en");
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var spring = await HistorySeed.HistoricAsync(_mongo.ConnectionString, tenant, "Spring ride", "Sofia", 10);
        var winter = await HistorySeed.HistoricAsync(_mongo.ConnectionString, tenant, "Winter ride", "Plovdiv", 200);
        var autumn = await HistorySeed.HistoricAsync(_mongo.ConnectionString, tenant, "Autumn ride", "Varna", 400);
        var latest = await HistorySeed.RideAsync(_mongo.ConnectionString, tenant, spring, 1, _ana, _wind, rank: 2);
        await HistorySeed.CountAlsoAsync(
            _mongo.ConnectionString,
            tenant,
            spring,
            "National Championship",
            latest,
            rank: 1,
            ParticipationCategory.JuniorOrYoungAdult
        );
        var withdrawn = await HistorySeed.RideAsync(
            _mongo.ConnectionString,
            tenant,
            winter,
            1,
            _ana,
            _fire,
            eliminated: new Retired()
        );
        var counted = await HistorySeed.RideAsync(
            _mongo.ConnectionString,
            tenant,
            autumn,
            1,
            _ana,
            _fire,
            notRanked: true,
            competition: "CEI 2*"
        );
        var service = Open(api, person, out _);

        var first = await service.HistoryOf(ParticipantKind.Athlete, _ana.Id, 1, 2, CancellationToken.None);
        var second = await service.HistoryOf(ParticipantKind.Athlete, _ana.Id, 2, 2, CancellationToken.None);

        Assert.True(first.HasMore);
        Assert.False(second.HasMore);
        Assert.Equal([latest.Id, withdrawn.Id, counted.Id], first.Items.Concat(second.Items).Select(x => x.Id));
        var newest = first.Items[0];
        Assert.Equal(
            (spring, "Spring ride", "Sofia", "CEI 1*"),
            (newest.EventId, newest.EventName, newest.Location, newest.Competition)
        );
        Assert.InRange(newest.StartDay, DateTimeOffset.UtcNow.AddDays(-12), DateTimeOffset.UtcNow.AddDays(-9));
        Assert.Equal(
            (ParticipantKind.Horse, _wind.Id, "Wind"),
            (newest.Other.Kind, newest.Other.Id, newest.Other.DisplayName)
        );
        Assert.Equal(Placing.Ranked(2), newest.Placing);
        var also = Assert.Single(newest.AlsoCountedIn);
        Assert.Equal(
            ("National Championship", "JuniorOrYoungAdult", Placing.Ranked(1)),
            (also.Name, also.Category, also.Placing)
        );
        Assert.Equal(Placing.Eliminated("RET"), first.Items[1].Placing);
        Assert.Empty(first.Items[1].AlsoCountedIn);
        var oldest = second.Items.Single();
        Assert.Equal(Placing.NotRanked(), oldest.Placing);
        Assert.Equal("CEI 2*", oldest.Competition);
    }

    [Fact]
    public async Task The_history_of_a_Horse_has_the_Athlete_as_the_other_half_and_a_ride_with_no_placing_has_none()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("en");
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var past = await HistorySeed.HistoricAsync(_mongo.ConnectionString, tenant, "Spring ride", "Sofia", 10);
        var ride = HistorySeed.Ride(past, 1, _ana, _fire); // no Ranking counts it
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, ride);
        var service = Open(api, person, out _);

        var page = await service.HistoryOf(ParticipantKind.Horse, _fire.Id, 1, 20, CancellationToken.None);

        var only = Assert.Single(page.Items);
        Assert.False(page.HasMore);
        Assert.Equal(ride.Id, only.Id);
        Assert.Equal(
            (ParticipantKind.Athlete, _ana.Id, "Mariela Testova"),
            (only.Other.Kind, only.Other.Id, only.Other.DisplayName)
        );
        Assert.Null(only.Placing);
        Assert.Empty(only.AlsoCountedIn);
    }

    [Fact]
    public async Task The_Horses_an_Athlete_has_ridden_are_the_other_halves_of_their_history_once_each_newest_first()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("en");
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var recent = await HistorySeed.HistoricAsync(_mongo.ConnectionString, tenant, "Spring ride", "Sofia", 10);
        var middle = await HistorySeed.HistoricAsync(_mongo.ConnectionString, tenant, "Winter ride", "Plovdiv", 200);
        var old = await HistorySeed.HistoricAsync(_mongo.ConnectionString, tenant, "Autumn ride", "Varna", 400);
        await HistorySeed.RideAsync(_mongo.ConnectionString, tenant, recent, 1, _ana, _wind);
        await HistorySeed.RideAsync(_mongo.ConnectionString, tenant, middle, 1, _ana, _fire);
        await HistorySeed.RideAsync(_mongo.ConnectionString, tenant, old, 1, _ana, _wind);
        var service = Open(api, person, out var asked);
        asked.Before = request =>
        {
            // the Api cuts the pages of the history at two, so that the whole of it is more than one
            request.RequestUri = new Uri(
                request.RequestUri!.ToString().Replace("page%5Bsize%5D=500", "page%5Bsize%5D=2")
            );
            return Task.CompletedTask;
        };

        var horses = await service.HorsesRiddenBy(_ana.Id, CancellationToken.None);

        Assert.Equal(
            [(ParticipantKind.Horse, _wind.Id, "Wind"), (ParticipantKind.Horse, _fire.Id, "Fire")],
            horses.Select(x => (x.Kind, x.Id, x.DisplayName))
        );
        Assert.Equal(2, asked.Asked.Count(x => x.Contains("participations/history", StringComparison.Ordinal))); // two pages of it
    }

    [Fact]
    public async Task A_Participation_of_a_history_is_read_with_its_Phases_by_its_id_and_one_that_is_not_there_is_none()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var past = await HistorySeed.HistoricAsync(_mongo.ConnectionString, tenant, "Spring ride", "Sofia", 10);
        var ride = await HistorySeed.RideAsync(_mongo.ConnectionString, tenant, past, 7, _ana, _fire);
        var service = Open(api, person, out _);

        var read = await service.ParticipationOf(ride.Id);
        var missing = await service.ParticipationOf(Guid.NewGuid());

        Assert.False(read.Failed);
        Assert.NotNull(read.Row);
        Assert.Equal((ride.Id, 7), (read.Row.Id, read.Row.Combination.Number));
        Assert.Single(read.Row.Phases);
        Assert.NotNull(read.Row.Phases.Single().ArriveTime);
        Assert.Null(missing.Row);
        Assert.False(missing.Failed); // a Participation that is not there is not a read that failed
    }

    [Fact]
    public async Task A_Participation_the_Api_does_not_answer_for_is_a_read_that_failed_and_not_one_that_is_not_there()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var past = await HistorySeed.HistoricAsync(_mongo.ConnectionString, tenant, "Spring ride", "Sofia", 10);
        var ride = await HistorySeed.RideAsync(_mongo.ConnectionString, tenant, past, 7, _ana, _fire);
        var service = Open(api, person, out var asked);
        asked.Answer = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

        var read = await service.ParticipationOf(ride.Id);

        Assert.True(read.Failed);
        Assert.Null(read.Row);
    }

    [Fact]
    public async Task A_text_of_three_characters_is_asked_of_the_registry_of_the_kind()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var service = Open(api, person, out var asked);

        var athletes = await service.Search(ParticipantKind.Athlete, " qzx ", 1, CancellationToken.None);
        var horses = await service.Search(ParticipantKind.Horse, " qzx ", 1, CancellationToken.None);

        Assert.Empty(athletes.Items);
        Assert.Empty(horses.Items);
        Assert.Single(
            asked.Asked,
            x => x.StartsWith("GET /api/athletes/all-tenants?filter=", StringComparison.Ordinal)
        );
        Assert.Single(asked.Asked, x => x.StartsWith("GET /api/horses/all-tenants?filter=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_name_with_an_apostrophe_is_found_by_it()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("en");
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var (latin, _) = HistorySeed.NameMarker();
        var athlete = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            $"Shane O'Brien {latin}",
            RegistrySeed.CountryOf("Ireland", "IE")
        );
        var service = Open(api, person, out _);

        var found = await service.Search(ParticipantKind.Athlete, $"o'brien {latin}", 1, CancellationToken.None);

        Assert.Equal([athlete], found.Items.Select(x => x.Id));
    }

    [Fact]
    public async Task Every_request_carries_the_language_the_app_is_in_and_none_when_it_is_in_none()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, home: null);
        var service = Open(api, person, out var asked);
        var languages = new List<string>();
        asked.Before = request =>
        {
            languages.AddRange(request.Headers.AcceptLanguage.Select(x => x.Value!));
            return Task.CompletedTask;
        };

        CultureInfo.CurrentUICulture = new CultureInfo("bg-BG");
        await service.Search(ParticipantKind.Athlete, "abc", 1, CancellationToken.None);
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        await service.Search(ParticipantKind.Athlete, "abc", 1, CancellationToken.None);

        Assert.Equal(["bg-BG"], languages);
    }

    [Fact]
    public async Task A_history_of_more_than_a_page_is_a_page_of_the_size_asked_for_and_the_rest()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("en");
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenancySeed.TenantAsync(_mongo.ConnectionString);
        var person = await TenancySeed.SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var past = await HistorySeed.HistoricAsync(_mongo.ConnectionString, tenant, "Long ride", "Sofia", 10);
        for (var number = 1; number <= 21; number++)
        {
            await HistorySeed.RideAsync(_mongo.ConnectionString, tenant, past, number, _ana, _wind);
        }

        var service = Open(api, person, out _);

        var first = await service.HistoryOf(ParticipantKind.Athlete, _ana.Id, 1, 20, CancellationToken.None);
        var second = await service.HistoryOf(ParticipantKind.Athlete, _ana.Id, 2, 20, CancellationToken.None);

        Assert.Equal((20, true), (first.Items.Count, first.HasMore));
        Assert.Equal((1, false), (second.Items.Count, second.HasMore));
        Assert.Equal(21, first.Items.Concat(second.Items).Select(x => x.Id).Distinct().Count());
    }

    static ParticipantService Open(
        ApiFactory api,
        TenancySeed.Person person,
        out JsonApiClients.Requests asked,
        TimeProvider? time = null
    )
    {
        var json = JsonApiClients.Of(api, person, out asked);
        return new ParticipantService(json, new ParticipationApiRepository(json), time ?? TimeProvider.System);
    }

    static (ParticipantKind, Guid, string) Of(ParticipantName name)
    {
        return (name.Kind, name.Id, name.DisplayName);
    }
}
