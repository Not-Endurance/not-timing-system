using System.Text;
using Not.Application.Authentication.User;
using Not.Application.HTTP;
using Not.Serialization.JSON;
using Not.Structures;
using NTS.Application.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Tests.Integration.Infrastructure;
using NTS.Witness.Contracts.API;
using SetupAthlete = NTS.Domain.Setup.Aggregates.Athlete;
using SetupAthleteModel = NTS.Application.Contracts.Setup.Models.AthleteModel;
using SetupClub = NTS.Domain.Setup.Aggregates.Club;
using SetupClubModel = NTS.Application.Contracts.Setup.Models.ClubModel;
using SetupConfigureEvent = NTS.Domain.Setup.Aggregates.ConfigureEvent;
using SetupConfigureEventModel = NTS.Application.Contracts.Setup.Models.ConfigureEventModel;
using SetupHorse = NTS.Domain.Setup.Aggregates.Horse;
using SetupHorseModel = NTS.Application.Contracts.Setup.Models.HorseModel;

namespace NTS.Tests.Integration.Drivers;

internal sealed class NexusApiDriver : IDisposable
{
    readonly HttpClient _client;

    public NexusApiDriver(Uri baseUrl)
    {
        _client = new HttpClient { BaseAddress = baseUrl };
    }

    public Task<NUserModel> RegisterUser(IntegrationUser user)
    {
        return Send<NUserModel>(
            HttpMethod.Post,
            "api/users/register",
            new RegisterUserPaload(
                user.Email,
                user.Name,
                user.GivenName,
                user.Surname,
                user.CountryRegion,
                user.MiddleName,
                user.Club,
                user.FeiId,
                user.DisplayName
            )
        );
    }

    public Task<NUserModel?> ReadUser(string email)
    {
        var encodedEmail = Uri.EscapeDataString(email);
        return SendNullable<NUserModel>(HttpMethod.Get, $"api/users/{encodedEmail}");
    }

    public Task<NUserModel> UpdateUserProfile(string email, UpdateUserProfilePayload payload)
    {
        var encodedEmail = Uri.EscapeDataString(email);
        return Send<NUserModel>(HttpMethod.Patch, $"api/users/{encodedEmail}/profile", payload);
    }

    public Task Create(EventInformation eventInformation)
    {
        return Send(HttpMethod.Post, "api/event-information", EventInformationModel.From(eventInformation));
    }

    public Task Update(EventInformation eventInformation)
    {
        return Send(HttpMethod.Patch, "api/event-information", EventInformationModel.From(eventInformation));
    }

    public Task Create(Participation participation)
    {
        return Send(HttpMethod.Post, "api/participations", ParticipationModel.MapFrom(participation));
    }

    public Task Create(Official official)
    {
        return Send(HttpMethod.Post, "api/officials", OfficialModel.MapFrom(official));
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

    public Task CreateSetupConfigureEvent(SetupConfigureEvent setupEvent)
    {
        return Send(HttpMethod.Post, "api/configure-event", SetupConfigureEventModel.From(setupEvent));
    }

    public Task UpdateSetupConfigureEvent(SetupConfigureEvent setupEvent)
    {
        return Send(HttpMethod.Patch, "api/configure-event", SetupConfigureEventModel.From(setupEvent));
    }

    public async Task<EventInformation> ReadEventInformation(int eventId)
    {
        var model = await Send<EventInformationModel>(HttpMethod.Get, $"api/event-information/{eventId}");
        return model.MapToEntity();
    }

    public async Task<IReadOnlyList<EventInformation>> ReadActiveEventInformation()
    {
        var models = await Send<IEnumerable<EventInformationModel>>(HttpMethod.Get, "api/event-information/active");
        return models.Select(x => x.MapToEntity()).ToArray();
    }

    public async Task<IReadOnlyList<EventInformation>> ReadPastEventInformation()
    {
        var models = await Send<IEnumerable<EventInformationModel>>(HttpMethod.Get, "api/event-information/past");
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
            $"Nexus API did not persist participation {participationId} before timeout ({state})."
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

    async Task Send(HttpMethod method, string endpoint, object? payload = null)
    {
        var content = await SendCore(method, endpoint, payload);
        var result = content.FromJson<Result>();
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

        return result.Data ?? throw new InvalidOperationException($"Nexus API returned no payload for '{endpoint}'.");
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
