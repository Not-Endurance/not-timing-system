using System.Text.Json.Serialization;
using NoTiming.Api.JsonApi;
using NTS.Domain.Core.Aggregates.Participations.Entities;

namespace NoTiming.Api.Features.Snapshots;

/// <summary>
/// A Snapshot as the server recorded it and tells it (ADR-0013, ADR-0005): the time event it is, with the outcome it came
/// to, the Participation and the Phase it was recorded in and the time of the Phase it fed. An Update says in addition what
/// the Phase showed for that time before it and shows now, which are the same when it was rejected.
/// </summary>
internal sealed class SnapshotResource
{
    /// <summary>The id of the resource, which is the id of the Snapshot: it is the resource's own and not an attribute of it.</summary>
    [JsonIgnore]
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid ParticipationId { get; set; }

    /// <summary>The start number of the Combination.</summary>
    public int Number { get; set; }

    /// <summary>The time of the Phase the event feeds: the Arrive, the Present or the Represent time.</summary>
    public TimeSlot Slot { get; set; }

    /// <summary>The time the event carries: the one the device captured, or the one an Update gave.</summary>
    public DateTimeOffset Time { get; set; }

    public TimeEventOutcome Outcome { get; set; }
    public Guid PhaseId { get; set; }
    public string Gate { get; set; } = default!;
    public DateTimeOffset? RecordedAt { get; set; }

    /// <summary>What the Phase showed for the time before an Update. None for a Snapshot, and for an Update of an empty time.</summary>
    public DateTimeOffset? PreviousTime { get; set; }

    /// <summary>What the Phase shows for the time after an Update. None for a Snapshot.</summary>
    public DateTimeOffset? CurrentTime { get; set; }
}

/// <summary>
/// The answer to a Snapshot or to an Update of one: the status that says what came of the request, with the resource that
/// was recorded, or the error that says why nothing was. A Snapshot that was recorded is 201 whatever its outcome, because
/// the event is what was made and the outcome is a member of it; one that was recorded before is 200 with the first outcome.
/// </summary>
internal sealed class SnapshotAnswer
{
    public static SnapshotAnswer Recorded(int status, SnapshotResource resource)
    {
        return new SnapshotAnswer(status, resource, null);
    }

    public static SnapshotAnswer Failed(JsonApiFailure failure)
    {
        return new SnapshotAnswer(failure.Status, null, failure);
    }

    SnapshotAnswer(int status, SnapshotResource? resource, JsonApiFailure? failure)
    {
        Status = status;
        Resource = resource;
        Failure = failure;
    }

    public int Status { get; }
    public SnapshotResource? Resource { get; }
    public JsonApiFailure? Failure { get; }
}
