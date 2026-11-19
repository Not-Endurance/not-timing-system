using NC.Application.HTTP;
using NC.Domain.Exceptions;
using NC.Exceptions;
using NC.Notify;
using NC.Structures;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Core.Commands;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Application.Contracts.Watcher.Models;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Aggregates.Participations;

namespace NoTiming.Storage.Core.Repositories;

public class ParticipationCommandApiClient : IParticipationCommands
{
    readonly NHttpClient _client;
    readonly INoTimingSocketContext _socketContext;

    public ParticipationCommandApiClient(NHttpClient client, INoTimingSocketContext socketContext)
    {
        _client = client;
        _socketContext = socketContext;
    }

    public Task<Participation?> UpdatePhase(PhaseUpdateModel model)
    {
        return Send(x => $"participations/{x}/phase", model);
    }

    public Task<Participation?> SetRepresentation(int participationId, bool isRequested)
    {
        return Send(x => $"participations/{x}/{participationId}/representation", new ToggleModel { IsRequested = isRequested });
    }

    public Task<Participation?> SetInspection(int participationId, bool isRequested)
    {
        return Send(x => $"participations/{x}/{participationId}/inspection", new ToggleModel { IsRequested = isRequested });
    }

    public Task<Participation?> Withdraw(int participationId)
    {
        return Send(x => $"participations/{x}/{participationId}/withdraw", null);
    }

    public Task<Participation?> Retire(int participationId)
    {
        return Send(x => $"participations/{x}/{participationId}/retire", null);
    }

    public Task<Participation?> FinishNotRanked(int participationId, string reason)
    {
        return Send(x => $"participations/{x}/{participationId}/finish-not-ranked", new FinishNotRankedModel { Reason = reason });
    }

    public Task<Participation?> Disqualify(int participationId, DisqualifyCode[] codes, string? reason)
    {
        return Send(
            x => $"participations/{x}/{participationId}/disqualify",
            new DisqualifyModel { Codes = codes, Reason = reason }
        );
    }

    public Task<Participation?> FailToQualify(int participationId, FailToQualifyCode[] codes, string? reason)
    {
        return Send(
            x => $"participations/{x}/{participationId}/fail-to-qualify",
            new FailToQualifyModel { Codes = codes, Reason = reason }
        );
    }

    public Task<Participation?> RestoreQualification(int participationId)
    {
        return Send(x => $"participations/{x}/{participationId}/restore", null);
    }

    public async Task<IReadOnlyList<Participation>> RecordSnapshots(SnapshotGroupModel group)
    {
        var eventId = EventId();
        try
        {
            var result = await _client.Post<ParticipationModel[]>($"participations/{eventId}/snapshots", group);
            if (!result.IsSuccess)
            {
                NotificationHelper.Current?.Warn(string.Join(Environment.NewLine, result.Errors));
                return [];
            }

            return (result.Data ?? []).Select(x => x.MapToEntity()).ToArray();
        }
        catch (Exception ex)
        {
            NotificationHelper.Current?.Error(ex);
            return [];
        }
    }

    Task<Participation?> Send(Func<int, string> endpoint, object? payload)
    {
        return SendCore(endpoint(EventId()), payload);
    }

    async Task<Participation?> SendCore(string endpoint, object? payload)
    {
        Result<ParticipationModel> result;
        try
        {
            result = await _client.Post<ParticipationModel>(endpoint, payload ?? new object());
        }
        catch (Exception ex)
        {
            NotificationHelper.Current?.Error(ex);
            return null;
        }

        if (!result.IsSuccess)
        {
            // The API refused the command. A judge must see the rule, as the domain's own refusals always showed.
            throw new DomainException(string.Join(Environment.NewLine, result.Errors));
        }

        return result.Data?.MapToEntity();
    }

    int EventId()
    {
        var eventId = _socketContext.Event?.Id;
        GuardHelper.ThrowIfDefault(eventId, "Cannot send a participation command before selecting an event.");
        return eventId.Value;
    }
}
