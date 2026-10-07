using NTS.Domain.Core.Aggregates.Participations.Entities;

namespace NTS.Contracts.Features.Snapshots;

/// <summary>
/// A Snapshot as the server recorded it (#644, ADR-0013), as the Api tells it: the time event it is, the outcome it came to,
/// and the Participation and the Phase it was recorded in. An Update says in addition the time the Phase showed before it and
/// shows now, which are the same when it was rejected.
/// </summary>
public sealed class RecordedSnapshot
{
    public Guid EventId { get; init; }
    public Guid ParticipationId { get; init; }

    /// <summary>The start number of the Combination.</summary>
    public int Number { get; init; }

    /// <summary>The time of the Phase the event feeds: the Arrive, the Present or the Represent time.</summary>
    public TimeSlot Slot { get; init; }

    /// <summary>The time the event carries: the one the device captured, or the one an Update gave.</summary>
    public DateTimeOffset Time { get; init; }

    public TimeEventOutcome Outcome { get; init; }
    public Guid PhaseId { get; init; }
    public string? Gate { get; init; }
    public DateTimeOffset? RecordedAt { get; init; }
    public DateTimeOffset? PreviousTime { get; init; }
    public DateTimeOffset? CurrentTime { get; init; }
}

/// <summary>
/// What the Api answered for one Snapshot that was sent, or one Update of it (#644): the status it said, and the Snapshot as
/// the server recorded it, or the error that says why nothing was. A Snapshot that was recorded is answered whatever its
/// outcome: a rejected one is a time event too, with its reason in <see cref="RecordedSnapshot.Outcome"/>. A Snapshot that
/// is not answered, because the request failed or the server refused it, is not recorded and is sent again.
/// </summary>
public sealed class SnapshotReceipt
{
    public static SnapshotReceipt Recorded(Guid id, int status, RecordedSnapshot snapshot)
    {
        return new SnapshotReceipt(id, status, snapshot, null, null);
    }

    public static SnapshotReceipt Failed(Guid id, int status, string? code, string message)
    {
        return new SnapshotReceipt(id, status, null, code, message);
    }

    SnapshotReceipt(Guid id, int status, RecordedSnapshot? snapshot, string? errorCode, string? errorMessage)
    {
        Id = id;
        Status = status;
        Snapshot = snapshot;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>The id of the Snapshot, which is the id of its time event.</summary>
    public Guid Id { get; }

    public int Status { get; }
    public RecordedSnapshot? Snapshot { get; }

    /// <summary>The stable code of the error, such as <c>event-ended</c>, when the Snapshot was not recorded.</summary>
    public string? ErrorCode { get; }

    /// <summary>What to tell a person when the Snapshot was not recorded.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Whether the server recorded the Snapshot, now or before: the Snapshot is no longer the device's to keep.</summary>
    public bool IsRecorded => Snapshot != null;

    /// <summary>Whether the time was taken: recorded, and not rejected.</summary>
    public bool IsAccepted => Snapshot?.Outcome == TimeEventOutcome.Accepted;
}
