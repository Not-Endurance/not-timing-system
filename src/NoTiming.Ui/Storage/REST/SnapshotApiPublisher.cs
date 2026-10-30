using System.Net;
using System.Text.Json;
using Not.Application.HTTP;
using Not.Injection;
using NoTiming.Ui.Features.Core.Dashboard;
using NTS.Contracts.Features.Snapshots;
using NTS.Domain.Core.Objects.Snapshots;

namespace NoTiming.Ui.Storage.REST;

/// <summary>
/// Sends the times the Ui captured to the Api (#644, ADR-0013): the <c>snapshots</c> resource. The device makes the id of each
/// Snapshot, which is the id of the time event the server records it as, so a group that is sent again is answered with the
/// first outcomes and records nothing twice. A group is sent in one request, in order, and the Api answers for each of its
/// Snapshots, so that one that failed does not take the rest with it. The session is the cookie the browser keeps.
/// </summary>
public sealed class SnapshotApiPublisher : ISnapshotPublisher, IScoped
{
    /// <summary>What the Api takes in one request.</summary>
    const int MOST_IN_A_REQUEST = 100;

    readonly JsonApiClient _client;

    public SnapshotApiPublisher(JsonApiClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<SnapshotReceipt>> PublishSnapshotsAsync(
        Guid eventId,
        SnapshotGroup snapshotGroup,
        CancellationToken cancellationToken = default
    )
    {
        var receipts = new List<SnapshotReceipt>();
        foreach (var part in snapshotGroup.Entries.Chunk(MOST_IN_A_REQUEST))
        {
            receipts.AddRange(await SendAsync(eventId, snapshotGroup, part, cancellationToken));
        }

        return receipts;
    }

    public async Task<SnapshotReceipt> UpdateSnapshotAsync(
        Guid snapshotId,
        DateTimeOffset time,
        CancellationToken cancellationToken = default
    )
    {
        var document = JsonSerializer.SerializeToElement(
            new
            {
                data = new
                {
                    type = "snapshots",
                    id = snapshotId.ToString(),
                    attributes = new { time },
                },
            },
            JsonApiClient.WriteOptions
        );

        var response = await _client.Send(HttpMethod.Patch, $"snapshots/{snapshotId}", document, cancellationToken);
        return ReceiptOf(snapshotId, (int)response.Status, response.Document, response.Error);
    }

    async Task<IEnumerable<SnapshotReceipt>> SendAsync(
        Guid eventId,
        SnapshotGroup group,
        Snapshot[] snapshots,
        CancellationToken cancellationToken
    )
    {
        var document = JsonSerializer.SerializeToElement(
            new
            {
                data = snapshots.Select(x => new
                {
                    type = "snapshots",
                    id = group.IdOf(x).ToString(),
                    attributes = new
                    {
                        eventId,
                        number = x.Number,
                        kind = group.Type,
                        time = x.Timestamp!.ToDateTimeOffset(),
                    },
                }),
            },
            JsonApiClient.WriteOptions
        );

        var response = await _client.Send(HttpMethod.Post, "snapshots/actions/send-group", document, cancellationToken);
        if (!response.IsSuccess)
        {
            // The request itself was refused (not signed in, a body that is not a group): none of its Snapshots was recorded.
            return snapshots.Select(x => Failed(group.IdOf(x), (int)response.Status, response.Error));
        }

        var results =
            response.Document is { } body
            && body.TryGetProperty("meta", out var meta)
            && meta.TryGetProperty("results", out var list)
            && list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray().ToArray()
                : [];
        return snapshots.Select(
            (x, index) =>
                index < results.Length
                    ? ReceiptOfResult(group.IdOf(x), results[index])
                    : Failed(group.IdOf(x), (int)HttpStatusCode.BadGateway, null)
        );
    }

    /// <summary>The answer for one entry of a group: its status, and its resource or its error, as the entry would have had alone.</summary>
    static SnapshotReceipt ReceiptOfResult(Guid id, JsonElement result)
    {
        var status = result.TryGetProperty("status", out var said) && said.TryGetInt32(out var number) ? number : 0;
        if (result.TryGetProperty("data", out var data))
        {
            return Recorded(id, status, data);
        }

        var error =
            result.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array
                ? errors.EnumerateArray().FirstOrDefault()
                : default;
        return Failed(
            id,
            status,
            new JsonApiError(status, Text(error, "code"), Text(error, "title"), Text(error, "detail"))
        );
    }

    static SnapshotReceipt ReceiptOf(Guid id, int status, JsonElement? document, JsonApiError? error)
    {
        return error != null || document is not { } body || !body.TryGetProperty("data", out var data)
            ? Failed(id, status, error)
            : Recorded(id, status, data);
    }

    static SnapshotReceipt Recorded(Guid id, int status, JsonElement data)
    {
        var recorded = data.TryGetProperty("attributes", out var attributes)
            ? attributes.Deserialize<RecordedSnapshot>(JsonApiClient.Options)
            : null;
        return recorded != null
            ? SnapshotReceipt.Recorded(id, status, recorded)
            : SnapshotReceipt.Failed(id, status, null, "The answer to the Snapshot could not be read.");
    }

    static SnapshotReceipt Failed(Guid id, int status, JsonApiError? error)
    {
        var said = error ?? new JsonApiError(status, null, null, null);
        return SnapshotReceipt.Failed(id, status, said.Code, said.Message);
    }

    static string? Text(JsonElement element, string member)
    {
        return
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(member, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
