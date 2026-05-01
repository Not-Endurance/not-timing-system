using System.Text;
using Not.Application.Authentication.User;
using Not.Serialization.JSON;
using Not.Structures;
using NTS.Application.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Tests.Integration.Infrastructure;
using NTS.Witness.Contracts.API;

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
