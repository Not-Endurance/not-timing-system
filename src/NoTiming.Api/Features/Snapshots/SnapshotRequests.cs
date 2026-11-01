using System.Text.Json;
using System.Text.Json.Serialization;
using NoTiming.Api.Features.Access;
using NoTiming.Api.JsonApi;
using NTS.Domain.Enums;

namespace NoTiming.Api.Features.Snapshots;

/// <summary>What a device says about a Snapshot it sends: where it was captured and when. A member that is not one of these is kept, to be refused.</summary>
internal sealed class SnapshotAttributes : IResourceAttributes, INamesEvent
{
    public static string? ResourceType => SnapshotDocuments.TYPE;

    public static DocumentIds Ids => DocumentIds.RequiredGuid;

    public static JsonApiFailure IdRequired => SnapshotFailures.IdRequired;

    public string? EventId { get; set; }
    public int? Number { get; set; }
    public string? Kind { get; set; }
    public DateTimeOffset? Time { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Others { get; set; }

    public JsonApiFailure? Invalid()
    {
        return SnapshotRequest.Problem(this);
    }
}

/// <summary>What an Official says in an Update of a Snapshot they sent: the time it should have had. A member that is not that is kept, to be refused.</summary>
internal sealed class SnapshotUpdateAttributes : IResourceAttributes
{
    public static string? ResourceType => SnapshotDocuments.TYPE;

    public static DocumentIds Ids => DocumentIds.MatchesRoute;

    public DateTimeOffset? Time { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Others { get; set; }

    public JsonApiFailure? Invalid()
    {
        if (SnapshotRequest.Unsupported(Others) is { } unsupported)
        {
            return unsupported;
        }

        return Time is null
            ? SnapshotRequest.Invalid("Say the time the Snapshot should have had: an instant with its offset.")
            : null;
    }
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
        if (Problem(attributes) is { } problem)
        {
            return (null, problem);
        }

        if (id is null)
        {
            return (null, SnapshotAttributes.IdRequired);
        }

        return !Guid.TryParse(id, out var snapshotId) || snapshotId == Guid.Empty
            ? (null, JsonApiFailures.InvalidId)
            : (Of(snapshotId, attributes), null);
    }

    /// <summary>The Snapshot of attributes that are valid, with the id its device made for it.</summary>
    public static SnapshotRequest Of(Guid id, SnapshotAttributes attributes)
    {
        return new SnapshotRequest(
            id,
            Guid.Parse(attributes.EventId!),
            attributes.Number!.Value,
            KindNamed(attributes.Kind)!.Value,
            attributes.Time!.Value
        );
    }

    /// <summary>What is wrong with the members of a Snapshot, none when they are valid.</summary>
    public static JsonApiFailure? Problem(SnapshotAttributes attributes)
    {
        if (Unsupported(attributes.Others) is { } unsupported)
        {
            return unsupported;
        }

        if (!Guid.TryParse(attributes.EventId, out var eventId) || eventId == Guid.Empty)
        {
            return SnapshotFailures.EventRequired;
        }

        if (attributes.Number is not { } number || number < 1)
        {
            return Invalid("The start number is a whole number from 1.");
        }

        if (KindNamed(attributes.Kind) is null)
        {
            return Invalid("The kind is Arrive, Present or Final.");
        }

        return attributes.Time is null
            ? Invalid("Say the time the Snapshot was captured: an instant with its offset.")
            : null;
    }

    /// <summary>The refusal of a member that is not one of the resource's, none when there is none.</summary>
    public static JsonApiFailure? Unsupported(Dictionary<string, JsonElement>? others)
    {
        return others is { Count: > 0 }
            ? JsonApiFailures.UnsupportedAttribute.With(string.Join(", ", others.Keys))
            : null;
    }

    public static JsonApiFailure Invalid(string detail)
    {
        return SnapshotFailures.InvalidSnapshot.With(detail);
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
