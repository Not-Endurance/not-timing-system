using NC.Domain.Exceptions;
using NC.Storage.Mongo;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Aggregates.Participations;
using NoTiming.Domain.Enums;

namespace NoTiming.Api.Application.Features.Participations;

/// <summary>
/// Runs participation commands. Judges send a command and receive the new state. The API is the only writer.
/// </summary>
public sealed class ParticipationCommandService
{
    readonly IParticipationChangeRepository _changes;
    readonly EventWriteLocks _locks;
    readonly IMongoRepository<ParticipationModel> _participations;

    public ParticipationCommandService(
        IMongoRepository<ParticipationModel> participations,
        IParticipationChangeRepository changes,
        EventWriteLocks locks
    )
    {
        _participations = participations;
        _changes = changes;
        _locks = locks;
    }

    public Task<ParticipationModel> UpdatePhase(int eventId, PhaseUpdateModel model)
    {
        var phaseId = model.Id ?? default;
        return _locks.Run(
            eventId,
            async () =>
            {
                var document = await ReadByPhase(eventId, phaseId);
                return await Apply(document, participation => participation.Update(model));
            }
        );
    }

    public Task<ParticipationModel> SetRepresentation(int eventId, int participationId, bool isRequested)
    {
        return Change(eventId, participationId, participation => participation.ToggleRepresentation(isRequested));
    }

    public Task<ParticipationModel> SetInspection(int eventId, int participationId, bool isRequested)
    {
        return Change(eventId, participationId, participation => participation.ToggleInspection(isRequested));
    }

    public Task<ParticipationModel> Withdraw(int eventId, int participationId)
    {
        return Change(eventId, participationId, participation => participation.Withdraw());
    }

    public Task<ParticipationModel> Retire(int eventId, int participationId)
    {
        return Change(eventId, participationId, participation => participation.Retire());
    }

    public Task<ParticipationModel> FinishNotRanked(int eventId, int participationId, string reason)
    {
        return Change(eventId, participationId, participation => participation.FinishNotRanked(reason));
    }

    public Task<ParticipationModel> Disqualify(int eventId, int participationId, DisqualifyCode[] codes, string? reason)
    {
        return Change(eventId, participationId, participation => participation.Disqualify(codes, reason));
    }

    public Task<ParticipationModel> FailToQualify(
        int eventId,
        int participationId,
        FailToQualifyCode[] codes,
        string? reason
    )
    {
        return Change(eventId, participationId, participation => participation.FailToQualify(codes, reason));
    }

    public Task<ParticipationModel> RestoreQualification(int eventId, int participationId)
    {
        return Change(eventId, participationId, participation => participation.Restore());
    }

    Task<ParticipationModel> Change(int eventId, int participationId, Action<Participation> change)
    {
        return _locks.Run(
            eventId,
            async () =>
            {
                var document = await ReadById(eventId, participationId);
                return await Apply(document, change);
            }
        );
    }

    async Task<ParticipationModel> ReadById(int eventId, int participationId)
    {
        var document = await _participations.Read(x => x.EventId == eventId && x.Id == participationId);
        return document
            ?? throw new DomainException($"Participation '{participationId}' was not found in event '{eventId}'.");
    }

    async Task<ParticipationModel> ReadByPhase(int eventId, int phaseId)
    {
        var document = await _participations.Read(x => x.EventId == eventId && x.Phases.Any(y => y.Id == phaseId));
        return document ?? throw new DomainException($"Phase '{phaseId}' was not found in event '{eventId}'.");
    }

    async Task<ParticipationModel> Apply(ParticipationModel document, Action<Participation> change)
    {
        var participation = document.MapToEntity();
        change(participation);
        var model = ParticipationModel.MapFrom(participation);
        await _changes.UpdateAndAnnounceChange(model);
        return model;
    }
}
