using System.Text;
using Not.Application.Authentication.User;
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
using SetupHorse = NTS.Domain.Setup.Aggregates.Horse;
using SetupHorseModel = NTS.Application.Contracts.Setup.Models.HorseModel;
using SetupUpcomingEvent = NTS.Domain.Setup.Aggregates.UpcomingEvent;
using SetupUpcomingEventModel = NTS.Application.Contracts.Setup.Models.UpcomingEventModel;

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
            new RegisterUserPaload(user.Email, user.Name)
        );
    }

    public Task Create(EnduranceEvent enduranceEvent)
    {
        return Send(HttpMethod.Post, "api/endurance-event", EnduranceEventModel.From(enduranceEvent));
    }

    public Task Create(Participation participation)
    {
        return Send(
            HttpMethod.Post,
            $"api/events/{participation.EventId}/participations",
            ParticipationModel.MapFrom(participation)
        );
    }

    public Task Create(Official official)
    {
        return Send(HttpMethod.Post, $"api/events/{official.EventId}/officials", OfficialModel.MapFrom(official));
    }

    public Task CreateSetupUpcomingEvent(SetupUpcomingEvent setupEvent)
    {
        return Send(HttpMethod.Post, "api/upcoming-event", SetupUpcomingEventModel.From(setupEvent));
    }

    public async Task<EnduranceEvent> ReadEnduranceEvent(int eventId)
    {
        var model = await Send<EnduranceEventModel>(HttpMethod.Get, $"api/endurance-event/{eventId}");
        return model.MapToEntity();
    }

    public async Task<Participation> ReadParticipation(int eventId, int participationId)
    {
        var model = await Send<ParticipationModel>(
            HttpMethod.Get,
            $"api/events/{eventId}/participations/{participationId}"
        );
        return model.MapToEntity();
    }

    public async Task<IReadOnlyList<Participation>> ReadParticipations(int eventId)
    {
        var models = await Send<IEnumerable<ParticipationModel>>(HttpMethod.Get, $"api/events/{eventId}/participations");
        return models.Select(x => x.MapToEntity()).ToArray();
    }

    public async Task<IReadOnlyList<Ranking>> ReadRankings(int eventId)
    {
        var models = await Send<IEnumerable<RankingModel>>(HttpMethod.Get, $"api/events/{eventId}/rankings");
        return models.Select(x => x.MapToEntity()).ToArray();
    }

    public async Task<IReadOnlyList<Handout>> ReadHandouts(int eventId)
    {
        var models = await Send<IEnumerable<HandoutModel>>(HttpMethod.Get, $"api/events/{eventId}/handouts");
        return models.Select(x => x.MapToEntity()).ToArray();
    }

    public Task<string> ReadParticipationsRaw(int eventId)
    {
        return SendCore(HttpMethod.Get, $"api/events/{eventId}/participations", null);
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

        var state = lastParticipation == null
            ? "no participation was returned"
            : $"phase complete: {lastParticipation.Phases.Current.IsComplete()}";
        throw new TimeoutException($"Nexus API did not persist participation {participationId} before timeout ({state}).");
    }

    public async Task<IReadOnlyList<SnapshotResultModel>> ReadSnapshotResults(int eventId)
    {
        var models = await Send<IEnumerable<SnapshotResultModel>>(
            HttpMethod.Get,
            $"api/events/{eventId}/snapshot-results"
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

    public async Task<IReadOnlyList<SetupUpcomingEvent>> ReadSetupUpcomingEvents()
    {
        var models = await Send<IEnumerable<SetupUpcomingEventModel>>(HttpMethod.Get, "api/upcoming-event");
        return models.Select(x => x.MapToEntity()).ToArray();
    }

    public async Task<SetupUpcomingEvent> ReadSetupUpcomingEvent(int id)
    {
        var model = await Send<SetupUpcomingEventModel>(HttpMethod.Get, $"api/upcoming-event/{id}");
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
}
