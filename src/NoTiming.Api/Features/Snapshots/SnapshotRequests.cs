using System.Text.Json;
using System.Text.Json.Serialization;
using NoTiming.Api.JsonApi;
using NTS.Domain.Enums;

namespace NoTiming.Api.Features.Snapshots;

/// <summary>What a device says about a Snapshot it sends: where it was captured and when. A member that is not one of these is kept, to be refused.</summary>
internal sealed class SnapshotAttributes
{
    public Guid? EventId { get; set; }
    public int? Number { get; set; }
    public string? Kind { get; set; }
    public DateTimeOffset? Time { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Others { get; set; }
}

/// <summary>What an Official says in an Update of a Snapshot they sent: the time it should have had. A member that is not that is kept, to be refused.</summary>
internal sealed class SnapshotUpdateAttributes
{
    public DateTimeOffset? Time { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Others { get; set; }
}

/// <summary>
/// A Snapshot as the server takes it (ADR-0013): the id its device made for it, which is the id of the time event it is
/// recorded as and the key of a Snapshot sent again, the Event, the start number, the kind of time and the time the device
/// captured. Who sent it and when it was recorded are not here: they are the server's to say.
/// </summary>
internal sealed class SnapshotRequest
{
    /// <summary>The Snapshot a document describes, or what is wrong with it: nothing it names is ignored without saying so.</summary>
    public static (SnapshotRequest? Request, JsonApiFailure? Failure) From(string? id, SnapshotAttributes attributes)
    {
        if (Unsupported(attributes.Others) is { } unsupported)
        {
            return (null, unsupported);
        }

        if (id is null)
        {
            return (
                null,
                new JsonApiFailure(
                    StatusCodes.Status400BadRequest,
                    "id-required",
                    "A Snapshot has the id its device made for it.",
                    "Send a Guid as the id of the resource: it is how a Snapshot sent again is told from a new one."
                )
            );
        }

        if (!Guid.TryParse(id, out var snapshotId) || snapshotId == Guid.Empty)
        {
            return (null, InvalidId());
        }

        if (attributes.EventId is not { } eventId || eventId == Guid.Empty)
        {
            return (
                null,
                new JsonApiFailure(
                    StatusCodes.Status400BadRequest,
                    "event-required",
                    "Name the Event the Snapshot is of.",
                    "The Event is the eventId attribute of the document."
                )
            );
        }

        if (attributes.Number is not { } number || number < 1)
        {
            return (null, Invalid("The start number is a whole number from 1."));
        }

        if (KindNamed(attributes.Kind) is not { } kind)
        {
            return (null, Invalid("The kind is Arrive, Present or Final."));
        }

        if (attributes.Time is not { } time)
        {
            return (null, Invalid("Say the time the Snapshot was captured: an instant with its offset."));
        }

        return (new SnapshotRequest(snapshotId, eventId, number, kind, time), null);
    }

    /// <summary>The refusal of a member that is not one of the resource's, none when there is none.</summary>
    public static JsonApiFailure? Unsupported(Dictionary<string, JsonElement>? others)
    {
        return others is { Count: > 0 }
            ? new JsonApiFailure(
                StatusCodes.Status400BadRequest,
                "unsupported-attribute",
                "The document names a member that is not one of the resource's, or one that the server alone sets.",
                string.Join(", ", others.Keys)
            )
            : null;
    }

    public static JsonApiFailure Invalid(string detail)
    {
        return new JsonApiFailure(
            StatusCodes.Status400BadRequest,
            "invalid-snapshot",
            "The document is not a Snapshot.",
            detail
        );
    }

    public static JsonApiFailure InvalidId()
    {
        return new JsonApiFailure(StatusCodes.Status400BadRequest, "invalid-id", "The id of a resource is a Guid.");
    }

    SnapshotRequest(Guid id, Guid eventId, int number, SnapshotType kind, DateTimeOffset time)
    {
        Id = id;
        EventId = eventId;
        Number = number;
        Kind = kind;
        Time = time;
    }

    public Guid Id { get; }
    public Guid EventId { get; }
    public int Number { get; }
    public SnapshotType Kind { get; }
    public DateTimeOffset Time { get; }

    /// <summary>The kind a name says, whatever its case: only the three names, and not a number or a list of them that an enum would take.</summary>
    static SnapshotType? KindNamed(string? name)
    {
        return Enum.GetValues<SnapshotType>()
            .Cast<SnapshotType?>()
            .FirstOrDefault(x => string.Equals(x.ToString(), name, StringComparison.OrdinalIgnoreCase));
    }
}
