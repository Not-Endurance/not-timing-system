using System.Text.Json;
using NoTiming.Api.Features.Access;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Snapshots;

/// <summary>
/// The body of a group of Snapshots, read when the parameter is bound: the resources of one request, or the error that refuses
/// it. A group is sent to one Event (ADR-0012), the one the first Snapshot that names an Event gives, so that access is decided
/// once, before the handler; a Snapshot of the group that is of another Event is refused on its own.
/// </summary>
internal sealed class SnapshotGroupBody : IJsonApiInput
{
    const string ATTRIBUTES = "attributes";
    const string EVENT_ID = "eventId";

    public static async ValueTask<SnapshotGroupBody?> BindAsync(HttpContext context)
    {
        var (entries, error) = await JsonApiRequests.ReadListAsync(context.Request);
        return new SnapshotGroupBody(entries ?? [], error);
    }

    SnapshotGroupBody(IReadOnlyList<JsonElement> entries, IResult? error)
    {
        Entries = entries;
        Transport = error;
    }

    public IReadOnlyList<JsonElement> Entries { get; }

    public IResult? Transport { get; }

    public string? NamedEvent => Entries.Select(EventNamedBy).FirstOrDefault(x => x != null);

    public string? DocumentId => null;

    public JsonApiFailure? Content()
    {
        return Entries.Count > SnapshotDocuments.MAX_IN_A_GROUP ? SnapshotFailures.TooManySnapshots : null;
    }

    static string? EventNamedBy(JsonElement entry)
    {
        return TryGet(entry, ATTRIBUTES, out var attributes) && TryGet(attributes, EVENT_ID, out var named)
            ? named.ValueKind == JsonValueKind.String
                ? named.GetString()
                : null
            : null;
    }

    static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var member in element.EnumerateObject())
            {
                if (string.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = member.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
