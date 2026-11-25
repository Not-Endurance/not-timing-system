using System.Security.Claims;
using NC.Domain.Exceptions;
using NC.Storage.Mongo;
using NoTiming.Api.Application.Features.Participations;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Application.Contracts.Watcher.Models;
using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Objects;

namespace NoTiming.Api.Application.Features.Snapshots;

/// <summary>Applies snapshots to participations. Each applied snapshot is announced to clients.</summary>
public sealed class SnapshotApplicationService
{
    readonly SnapshotAuthorizer _authorizer;
    readonly IParticipationChangeRepository _changes;
    readonly ILogger<SnapshotApplicationService> _logger;
    readonly EventWriteLocks _locks;
    readonly IMongoRepository<ParticipationModel> _participations;

    public SnapshotApplicationService(
        SnapshotAuthorizer authorizer,
        IMongoRepository<ParticipationModel> participations,
        IParticipationChangeRepository changes,
        EventWriteLocks locks,
        ILogger<SnapshotApplicationService> logger
    )
    {
        _authorizer = authorizer;
        _participations = participations;
        _changes = changes;
        _logger = logger;
        _locks = locks;
    }

    /// <summary>A witness's snapshots. Only authorized event staff may send them.</summary>
    public async Task<IReadOnlyList<ParticipationModel>> ApplyFromWitness(
        ClaimsPrincipal? principal,
        int eventId,
        SnapshotGroupModel group
    )
    {
        await _authorizer.Authorize(principal, eventId);
        return await Apply(eventId, group);
    }

    /// <summary>A judge's own snapshot. Judges are not signed in, so they are not checked against staff.</summary>
    public Task<IReadOnlyList<ParticipationModel>> ApplyFromJudge(int eventId, SnapshotGroupModel group)
    {
        return Apply(eventId, group);
    }

    async Task<IReadOnlyList<ParticipationModel>> Apply(int eventId, SnapshotGroupModel group)
    {
        if (group.Entries.Length == 0)
        {
            return [];
        }

        return await _locks.Run(
            eventId,
            async () =>
            {
                var applied = new List<ParticipationModel>();
                foreach (var entry in group.Entries.Where(x => x.Timestamp != null))
                {
                    try
                    {
                        var model = await ApplyEntry(eventId, group.Type, entry);
                        if (model != null)
                        {
                            applied.Add(model);
                        }
                    }
                    catch (DomainException exception)
                    {
                        // A snapshot that breaks a timing rule stops the group, as it always has: the entries after it are
                        // not applied. The witness is still told the group was accepted.
                        _logger.LogWarning(
                            exception,
                            "Snapshot for #{Number} in event {EventId} was refused.",
                            entry.Number,
                            eventId
                        );
                        break;
                    }
                }

                return (IReadOnlyList<ParticipationModel>)applied;
            }
        );
    }

    async Task<ParticipationModel?> ApplyEntry(int eventId, SnapshotType type, SnapshotModel entry)
    {
        var document = await _participations.Read(x => x.EventId == eventId && x.Combination.Number == entry.Number);
        if (document == null)
        {
            return null;
        }

        var participation = document.MapToEntity();
        var snapshot = new Snapshot(entry.Number, type, SnapshotMethod.Manual, new Timestamp(entry.Timestamp!));
        var result = participation.Process(snapshot);
        if (result.Type != SnapshotResultType.Applied)
        {
            return null;
        }

        var model = ParticipationModel.MapFrom(participation);
        await _changes.UpdateAndAnnounceChange(model);
        return model;
    }
}
