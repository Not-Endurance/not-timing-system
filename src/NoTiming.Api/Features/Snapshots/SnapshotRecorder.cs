using System.Reflection;
using Not.Exceptions;
using Not.Identity;
using NoTiming.Api.Features.Events;
using NoTiming.Api.Features.Live;
using NoTiming.Api.Features.Reference;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Core.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NoTiming.Api.Features.Snapshots;

/// <summary>
/// The server records every time (#644, ADR-0013, ADR-0005): the Snapshot of an Official or an Operator of a Live Event is
/// placed by the rule of ADR-0004, recorded as a time event on the Participation with its outcome, saved, and announced to
/// the viewers of the Event, and an Update of one is recorded the same way. The caller was asked of the policy on every
/// request by the endpoint filter (#671), which reads the grants on it, so that a person whose access was removed is refused
/// on the next request, and the recorder is reached only for a caller who may send a Snapshot to the Event now.
/// <para>
/// A Participation is written against the version it was read at and the version is one more in the same write, so that of
/// two Snapshots for one Participation at once one is taken and the other finds the Participation as it is now: it is read
/// again, placed again, and written again, without the sender noticing. A Snapshot is recorded once whatever the number of
/// times it is sent: its id is the id of the event, and the same id again is the event that was recorded, with its outcome.
/// What is written is the Phases and the elimination of the Participation, which are what a time can change.
/// </para>
/// </summary>
internal sealed class SnapshotRecorder
{
    /// <summary>Enough for every Official of an Event to be waiting at once, and no more: a Participation that cannot be written is not retried for ever.</summary>
    const int MAX_ATTEMPTS = 25;
    static readonly ReferenceMembers<ParticipationModel> MEMBERS = new([], []);
    static readonly PropertyInfo[] CHANGED_BY_A_TIME =
    [
        typeof(ParticipationModel).GetProperty(nameof(ParticipationModel.Phases))!,
        typeof(ParticipationModel).GetProperty(nameof(ParticipationModel.Eliminated))!,
    ];

    readonly TenantCollections _tenants;
    readonly CrossTenantReads _reads;
    readonly IParticipationChanges _changes;
    readonly TimeProvider _time;

    public SnapshotRecorder(
        TenantCollections tenants,
        CrossTenantReads reads,
        IParticipationChanges changes,
        TimeProvider time
    )
    {
        _tenants = tenants;
        _reads = reads;
        _changes = changes;
        _time = time;
    }

    /// <summary>Records the Snapshot in the Event the caller was let in to, or says what it has been recorded as before, or why it cannot be.</summary>
    public async Task<SnapshotAnswer> RecordAsync(
        NIdentityUser user,
        SnapshotRequest request,
        EventFacts facts,
        CancellationToken cancellationToken
    )
    {
        var collection = ParticipationsOf(facts);
        var checkedElsewhere = false;
        for (var attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
        {
            var model = (
                await collection.ReadAsync(
                    x => x.EventId == request.EventId && x.Combination.Number == request.Number,
                    null,
                    0,
                    1,
                    cancellationToken
                )
            ).FirstOrDefault();
            if (model is null)
            {
                return SnapshotAnswer.Failed(
                    new JsonApiFailure(
                        StatusCodes.Status404NotFound,
                        "participation-not-found",
                        "The Event has no Participation with that start number."
                    )
                );
            }

            var participation = model.MapToEntity();
            var holding = participation.Phases.SelectMany(x => x.Events).FirstOrDefault(x => x.Id == request.Id);
            if (holding is SnapshotTimeEvent recordedBefore)
            {
                return SnapshotAnswer.Recorded(
                    StatusCodes.Status200OK,
                    Describe(participation, request.Id, recordedBefore)
                );
            }

            if (holding != null)
            {
                return SnapshotAnswer.Failed(IdTaken()); // an edit of the Phase form has that id: it is no Snapshot
            }

            if (!checkedElsewhere && await IsHeldByAnotherAsync(request.Id, participation.Id, cancellationToken))
            {
                return SnapshotAnswer.Failed(IdTaken());
            }

            checkedElsewhere = true;
            var snapshot = new Snapshot(
                request.Number,
                request.Kind,
                SnapshotMethod.Manual,
                new Timestamp(Milliseconds(request.Time)),
                request.Id
            );
            var recorded = participation.Process(snapshot, user.Id, Milliseconds(_time.GetUtcNow()));
            if (await WriteAsync(collection, model, participation, cancellationToken))
            {
                await _changes.AnnounceAsync(participation.EventId, participation.Id);
                return SnapshotAnswer.Recorded(
                    StatusCodes.Status201Created,
                    Describe(participation, request.Id, recorded)
                );
            }
        }

        return Busy();
    }

    /// <summary>
    /// Records an Update of a Snapshot that was sent: the time it should have had. The Snapshot was found by its id, which says
    /// which Participation holds it and so which Event, and the Update was asked of the policy like a Snapshot is.
    /// </summary>
    public async Task<SnapshotAnswer> UpdateAsync(
        NIdentityUser user,
        ParticipationModel held,
        Guid snapshotId,
        DateTimeOffset time,
        EventFacts facts,
        CancellationToken cancellationToken
    )
    {
        var collection = ParticipationsOf(facts);
        var model = held;
        for (var attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
        {
            if (attempt > 0)
            {
                model = await collection.FindAsync(held.Id, cancellationToken);
                if (model is null)
                {
                    return SnapshotAnswer.Failed(NotFound());
                }
            }

            var participation = model.MapToEntity();
            SnapshotUpdate update;
            try
            {
                update = participation.UpdateSnapshot(
                    snapshotId,
                    new Timestamp(Milliseconds(time)),
                    user.Id,
                    Milliseconds(_time.GetUtcNow())
                );
            }
            catch (GuardException)
            {
                return SnapshotAnswer.Failed(NotFound()); // the Participation no longer holds the Snapshot
            }

            if (await WriteAsync(collection, model, participation, cancellationToken))
            {
                await _changes.AnnounceAsync(participation.EventId, participation.Id);
                return SnapshotAnswer.Recorded(StatusCodes.Status200OK, Describe(participation, snapshotId, update));
            }
        }

        return Busy();
    }

    /// <summary>
    /// The instant to the millisecond, which is what the database keeps of it: the answer to a Snapshot sent again is read
    /// from the document, so what the first answer said has to be what the document holds.
    /// </summary>
    static DateTimeOffset Milliseconds(DateTimeOffset instant)
    {
        return new DateTimeOffset(instant.Ticks - instant.Ticks % TimeSpan.TicksPerMillisecond, instant.Offset);
    }

    static SnapshotResource Describe(Participation participation, Guid id, SnapshotTimeEvent timeEvent)
    {
        var phase = participation.Phases.First(x => x.Events.Contains(timeEvent));
        return DescribeIn(participation, id, phase, timeEvent, timeEvent.Slot);
    }

    static SnapshotResource Describe(Participation participation, Guid id, SnapshotUpdate update)
    {
        var resource = DescribeIn(participation, id, update.Phase, update.Event, update.Slot);
        resource.PreviousTime = update.Previous?.ToDateTimeOffset();
        resource.CurrentTime = update.Current?.ToDateTimeOffset();
        return resource;
    }

    static SnapshotResource DescribeIn(
        Participation participation,
        Guid id,
        Phase phase,
        SnapshotTimeEvent timeEvent,
        TimeSlot slot
    )
    {
        return new SnapshotResource
        {
            Id = id,
            EventId = participation.EventId,
            ParticipationId = participation.Id,
            Number = participation.Combination.Number,
            Slot = slot,
            Time = timeEvent.Time.ToDateTimeOffset(),
            Outcome = timeEvent.Outcome,
            PhaseId = phase.Id,
            Gate = phase.Gate,
            RecordedAt = timeEvent.RecordedAt,
        };
    }

    static JsonApiFailure IdTaken()
    {
        return new JsonApiFailure(
            StatusCodes.Status409Conflict,
            "id-taken",
            "An event with that id was recorded for another Participation, or is not a Snapshot."
        );
    }

    static JsonApiFailure NotFound()
    {
        return new JsonApiFailure(StatusCodes.Status404NotFound, "not-found", "Not found");
    }

    static SnapshotAnswer Busy()
    {
        return SnapshotAnswer.Failed(
            new JsonApiFailure(
                StatusCodes.Status409Conflict,
                "participation-busy",
                "The Participation was being changed by too many at once.",
                "Send it again: a Snapshot is recorded once whatever the number of times it is sent."
            )
        );
    }

    /// <summary>The write of what a time changed, against the version the Participation was read at: false when it was written since.</summary>
    static async Task<bool> WriteAsync(
        TypedTenantCollection<ParticipationModel> collection,
        ParticipationModel read,
        Participation changed,
        CancellationToken cancellationToken
    )
    {
        var update = MEMBERS.UpdateOf(ParticipationModel.MapFrom(changed), CHANGED_BY_A_TIME);
        return await collection.UpdateAtVersionAsync(read.Id, update, read.Version, cancellationToken);
    }

    TypedTenantCollection<ParticipationModel> ParticipationsOf(EventFacts facts)
    {
        return _tenants.Of<ParticipationModel>(TenantOwned.EVENT_PARTICIPATIONS, facts.TenantId);
    }

    /// <summary>Whether a Participation other than this one holds an event with the id: the id of a Snapshot is not another's to use.</summary>
    async Task<bool> IsHeldByAnotherAsync(Guid snapshotId, Guid participationId, CancellationToken cancellationToken)
    {
        var holder = await _reads.FindParticipationOfTimeEventAsync(snapshotId, cancellationToken);
        return holder != null && holder.Id != participationId;
    }
}
