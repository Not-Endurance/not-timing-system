using System.Text;
using NC.Application.Authentication.User;
using NC.Application.HTTP;
using NC.Serialization.JSON;
using NC.Structures;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Application.Contracts.Watcher.Models;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.IntegrationTests.Authentication.Harness;
using NoTiming.IntegrationTests.Infrastructure;
using NoTiming.Ui.Contracts.API;
using SetupAthlete = NoTiming.Domain.Setup.Aggregates.Athlete;
using SetupAthleteModel = NoTiming.Application.Contracts.Setup.Models.AthleteModel;
using SetupClub = NoTiming.Domain.Setup.Aggregates.Club;
using SetupClubModel = NoTiming.Application.Contracts.Setup.Models.ClubModel;
using SetupConfigureEvent = NoTiming.Domain.Setup.Aggregates.ConfigureEvent;
using SetupConfigureEventModel = NoTiming.Application.Contracts.Setup.Models.ConfigureEventModel;
using SetupHorse = NoTiming.Domain.Setup.Aggregates.Horse;
using SetupHorseModel = NoTiming.Application.Contracts.Setup.Models.HorseModel;

namespace NoTiming.IntegrationTests.Drivers;

internal sealed class NoTimingApiDriver : IDisposable
{
    readonly HttpClient _client;
    readonly NoTimingIntegrationFixture _fixture;

    public NoTimingApiDriver(NoTimingIntegrationFixture fixture)
    {
        _fixture = fixture;
        _client = new HttpClient { BaseAddress = fixture.NoTimingBaseUrl };
    }

    /// <summary>
    /// Makes the person's account the way a pre-identity row would be: seeded with the profile exactly as given, then
    /// proved by a code at sign-in, which adopts the row and keeps its id. Returns the signed-in person's profile.
    /// </summary>
    public async Task<NUserModel> RegisterUser(IntegrationUser user)
    {
        var mongo = new MongoAccess(_fixture.MongoConnectionString);
        if (!await mongo.UserExistsAsync(user.Email))
        {
            await mongo.SeedLegacyUserAsync(
                AccountTestData.NewId(),
                user.Email,
                user.Name,
                user.GivenName,
                user.MiddleName,
                user.Surname,
                user.CountryRegion,
                user.Club,
                user.FeiId,
                user.DisplayName
            );
        }

        var cookie = await _fixture.SignInAsync(user);
        return await ReadOwnProfile(cookie);
    }

    public async Task<NUserModel> ReadOwnProfile(string cookie)
    {
        var json = await SendSignedIn(HttpMethod.Get, "api/me/profile", payload: null, cookie);
        return json.FromJson<NUserModel>();
    }

    public Task UpdateOwnProfile(string cookie, UpdateUserProfilePayload payload)
    {
        return SendSignedIn(HttpMethod.Patch, "api/me", payload, cookie);
    }


    public Task Create(EventInformation eventInformation)
    {
        return Send(HttpMethod.Post, "api/event", EventInformationModel.From(eventInformation));
    }

    public Task Update(EventInformation eventInformation)
    {
        return Send(HttpMethod.Patch, "api/event", EventInformationModel.From(eventInformation));
    }

    public Task Create(Participation participation)
    {
        return Send(HttpMethod.Post, "api/participations", ParticipationModel.MapFrom(participation));
    }

    public Task Create(Official official)
    {
        return Send(HttpMethod.Post, "api/officials", OfficialModel.MapFrom(official));
    }

    public Task Create(Operator accessOperator)
    {
        return Send(HttpMethod.Post, "api/operators", OperatorModel.MapFrom(accessOperator));
    }

    public Task Create(Ranking ranking)
    {
        return Send(HttpMethod.Post, "api/rankings", RankingModel.From(ranking));
    }

    public Task Update(Ranking ranking)
    {
        return Send(HttpMethod.Patch, "api/rankings", RankingModel.From(ranking));
    }

    public Task Create(Handout handout)
    {
        return Send(HttpMethod.Post, "api/handouts", HandoutModel.From(handout));
    }

    public Task DeleteHandout(int id)
    {
        return Send(HttpMethod.Delete, $"api/handouts/{id}");
    }

    public Task CreateSetupConfigureEvent(SetupConfigureEvent setupEvent)
    {
        return Send(HttpMethod.Post, "api/configure-event", SetupConfigureEventModel.From(setupEvent));
    }

    public Task UpdateSetupConfigureEvent(SetupConfigureEvent setupEvent)
    {
        return Send(HttpMethod.Patch, "api/configure-event", SetupConfigureEventModel.From(setupEvent));
    }

    public async Task<EventInformation> StartEventInformation(int configureEventId)
    {
        var model = await Send<EventInformationModel>(HttpMethod.Post, $"api/event/{configureEventId}/start", new { });
        return model.MapToEntity();
    }

    public async Task<EventInformation> ReadEventInformation(int eventId)
    {
        var model = await Send<EventInformationModel>(HttpMethod.Get, $"api/event/{eventId}");
        return model.MapToEntity();
    }

    public async Task<IReadOnlyList<EventInformation>> ReadActiveEventInformation()
    {
        var models = await Send<IEnumerable<EventInformationModel>>(HttpMethod.Get, "api/event/active");
        return models.Select(x => x.MapToEntity()).ToArray();
    }

    public async Task<IReadOnlyList<EventInformation>> ReadPastEventInformation()
    {
        var models = await Send<IEnumerable<EventInformationModel>>(HttpMethod.Get, "api/event/past");
        return models.Select(x => x.MapToEntity()).ToArray();
    }

    public async Task<Participation> ReadParticipation(int eventId, int participationId)
    {
        var model = await Send<ParticipationModel>(
            HttpMethod.Get,
            EventFilter($"api/participations/{participationId}", eventId)
        );
        return model.MapToEntity();
    }

    public async Task<IReadOnlyList<Participation>> ReadParticipations(int eventId)
    {
        var models = await Send<IEnumerable<ParticipationModel>>(
            HttpMethod.Get,
            EventFilter("api/participations", eventId)
        );
        return models.Select(x => x.MapToEntity()).ToArray();
    }

    public async Task<IReadOnlyList<Ranking>> ReadRankings(int eventId)
    {
        var models = await Send<IEnumerable<RankingModel>>(HttpMethod.Get, EventFilter("api/rankings", eventId));
        return models.Select(x => x.MapToEntity()).ToArray();
    }

    public async Task<IReadOnlyList<Official>> ReadOfficials(int eventId)
    {
        var models = await Send<IEnumerable<OfficialModel>>(HttpMethod.Get, EventFilter("api/officials", eventId));
        return models.Select(x => x.MapToEntity()).ToArray();
    }

    public async Task<IReadOnlyList<Operator>> ReadOperators(int eventId)
    {
        var models = await Send<IEnumerable<OperatorModel>>(HttpMethod.Get, EventFilter("api/operators", eventId));
        return models.Select(x => x.MapToEntity()).ToArray();
    }

    public async Task<IReadOnlyList<Handout>> ReadHandouts(int eventId)
    {
        var models = await Send<IEnumerable<HandoutModel>>(HttpMethod.Get, EventFilter("api/handouts", eventId));
        return models.Select(x => x.MapToEntity()).ToArray();
    }

    public Task<string> ReadParticipationsRaw(int eventId)
    {
        return SendCore(HttpMethod.Get, EventFilter("api/participations", eventId), null);
    }

    public async Task<Participation> WaitForParticipation(
        int eventId,
        int participationId,
        Func<Participation, bool> predicate,
        TimeSpan timeout
    )
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        Participation? lastParticipation = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            lastParticipation = await ReadParticipation(eventId, participationId);
            if (predicate(lastParticipation))
            {
                return lastParticipation;
            }

            await Task.Delay(100);
        }

        var state =
            lastParticipation == null
                ? "no participation was returned"
                : $"phase complete: {lastParticipation.Phases.Current.IsComplete()}";
        throw new TimeoutException(
            $"NoTiming API did not persist participation {participationId} before timeout ({state})."
        );
    }

    public async Task<IReadOnlyList<SnapshotResultModel>> ReadSnapshotResults(int eventId)
    {
        var models = await Send<IEnumerable<SnapshotResultModel>>(
            HttpMethod.Get,
            EventFilter("api/snapshot-results", eventId)
        );
        return models.ToArray();
    }

    public async Task<NoTimingUserSessionModel?> ReadUserSession(string userIdentifier, int eventId, string cookie)
    {
        var encodedUserIdentifier = Uri.EscapeDataString(userIdentifier);
        var json = await SendSignedIn(
            HttpMethod.Get,
            $"api/user-sessions/{eventId}/by-user-identifier/{encodedUserIdentifier}",
            payload: null,
            cookie
        );
        var result = json.FromJson<Result<NoTimingUserSessionModel>>();
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors));
        }

        return result.Data;
    }

    public async Task<IReadOnlyList<SetupClub>> ReadSetupClubs()
    {
        var models = await Send<IEnumerable<SetupClubModel>>(HttpMethod.Get, "api/clubs");
        return models.Select(x => x.MapToEntity()).ToArray();
    }

    public async Task<IReadOnlyList<SetupHorse>> ReadSetupHorses()
    {
        var models = await Send<IEnumerable<SetupHorseModel>>(HttpMethod.Get, "api/horses");
        return models.Select(x => x.MapToEntity()).ToArray();
    }

    public async Task<IReadOnlyList<SetupAthlete>> ReadSetupAthletes()
    {
        var models = await Send<IEnumerable<SetupAthleteModel>>(HttpMethod.Get, "api/athletes");
        return models.Select(x => x.MapToEntity()).ToArray();
    }

    public async Task<IReadOnlyList<SetupConfigureEvent>> ReadSetupConfigureEvents()
    {
        var models = await Send<IEnumerable<SetupConfigureEventModel>>(HttpMethod.Get, "api/configure-event");
        return models.Select(x => x.MapToEntity()).ToArray();
    }

    public async Task<SetupConfigureEvent> ReadSetupConfigureEvent(int id)
    {
        var model = await Send<SetupConfigureEventModel>(HttpMethod.Get, $"api/configure-event/{id}");
        return model.MapToEntity();
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    async Task<string> SendSignedIn(HttpMethod method, string endpoint, object? payload, string cookie)
    {
        using var request = new HttpRequestMessage(method, endpoint);
        if (payload != null)
        {
            request.Content = new StringContent(payload.ToJson(), Encoding.UTF8, "application/json");
        }

        request.Headers.Add("Cookie", cookie);
        if (method != HttpMethod.Get)
        {
            request.Headers.Add("X-NTS-Request", "1");
        }

        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    async Task Send(HttpMethod method, string endpoint, object? payload = null)
    {
        var content = await SendCore(method, endpoint, payload);
        var result = content.FromJson<NC.Structures.Result>();
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors));
        }
    }

    async Task<T> Send<T>(HttpMethod method, string endpoint, object? payload = null)
        where T : class
    {
        var content = await SendCore(method, endpoint, payload);
        var result = content.FromJson<Result<T>>();
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors));
        }

        return result.Data
            ?? throw new InvalidOperationException($"NoTiming API returned no payload for '{endpoint}'.");
    }

    async Task<T?> SendNullable<T>(HttpMethod method, string endpoint, object? payload = null)
        where T : class
    {
        var content = await SendCore(method, endpoint, payload);
        var result = content.FromJson<Result<T>>();
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors));
        }

        return result.Data;
    }

    async Task<string> SendCore(HttpMethod method, string endpoint, object? payload)
    {
        using var request = new HttpRequestMessage(method, endpoint);
        if (payload != null)
        {
            request.Content = new StringContent(payload.ToJson(), Encoding.UTF8, "application/json");
        }

        using var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"{method} {endpoint} failed with {(int)response.StatusCode} ({response.ReasonPhrase}). {content}",
                null,
                response.StatusCode
            );
        }

        return content;
    }

    static string EventFilter(string endpoint, int eventId)
    {
        return HttpHelper.AddQueryString(
            endpoint,
            ODataApiFilterAdapter.ParseFilters<EventFilterDocument>([document => document.EventId == eventId])
        );
    }

    sealed class EventFilterDocument
    {
        public int EventId { get; set; }
    }
}
