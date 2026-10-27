using System.Text.Json;
using NoTiming.Api.Features.Access;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Core.Models;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.Snapshots;

/// <summary>
/// The routes of the Snapshots (#644, ADR-0013, the rest-api skill). <c>POST /api/snapshots</c> sends one: the id its device
/// made for it, the Event, the start number, the kind and the time it captured, and the answer is the time event the server
/// recorded, with its outcome: 201 whatever the outcome, because the event is what was made, and 200 with the first answer
/// for an id that was sent before. <c>POST /api/snapshots/actions/send-group</c> sends a list of them to one Event, processed
/// in order, each answered in its own result and none stopping the ones after it. <c>PATCH /api/snapshots/{id}</c> is an
/// Update of a Snapshot that was sent: the time it should have had, an absolute value, and the answer says what changed or
/// why not. Every route needs a signed-in caller, and whether the caller may send a Snapshot to the Event now is declared on
/// the handler and decided by the endpoint filter (#671), once for a group.
/// </summary>
internal static class SnapshotEndpoints
{
    public static IEndpointRouteBuilder MapSnapshots(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/snapshots", PostSnapshots);
        app.MapPost("/api/snapshots/actions/send-group", PostSnapshotsActionsSendGroup);
        app.MapPatch("/api/snapshots/{id}", PatchSnapshotsById).WithMetadata(new SnapshotRows());
        return app;
    }

    [Requires(Capability.SendSnapshot, PlaceIn.EventInBody)]
    static async Task<IResult> PostSnapshots(
        JsonApiBody<SnapshotAttributes> body,
        AccessContext access,
        SnapshotRecorder recorder,
        CancellationToken cancellationToken
    )
    {
        var request = SnapshotRequest.Of(Guid.Parse(body.Id!), body.Attributes);
        return AsResult(await recorder.RecordAsync(access.User, request, access.Event, cancellationToken));
    }

    [Requires(Capability.SendSnapshot, PlaceIn.EventInBody)]
    static async Task<IResult> PostSnapshotsActionsSendGroup(
        SnapshotGroupBody group,
        AccessContext access,
        SnapshotRecorder recorder,
        CancellationToken cancellationToken
    )
    {
        var results = new List<object>();
        foreach (var entry in group.Entries)
        {
            results.Add(ResultOf(await SendEntryAsync(entry, access, recorder, cancellationToken)));
        }

        return Results.Json(
            new { meta = new { results } },
            JsonApiResults.Options,
            JsonApiResults.MEDIA_TYPE,
            StatusCodes.Status200OK
        );
    }

    // The Snapshot is found by its id, which says which Participation holds it and so which Event it is of.
    [Requires(Capability.SendSnapshot, PlaceIn.EventOfRow)]
    static async Task<IResult> PatchSnapshotsById(
        string id,
        JsonApiBody<SnapshotUpdateAttributes> body,
        AccessContext access,
        SnapshotRecorder recorder,
        CancellationToken cancellationToken
    )
    {
        return AsResult(
            await recorder.UpdateAsync(
                access.User,
                access.RowOf<ParticipationModel>(),
                Guid.Parse(id),
                body.Attributes.Time!.Value,
                access.Event,
                cancellationToken
            )
        );
    }

    /// <summary>One entry of a group: read as a Snapshot and sent, or the failure that says what is wrong with it.</summary>
    static async Task<SnapshotAnswer> SendEntryAsync(
        JsonElement entry,
        AccessContext access,
        SnapshotRecorder recorder,
        CancellationToken cancellationToken
    )
    {
        JsonApiResourceObject<SnapshotAttributes>? resource;
        try
        {
            resource = entry.Deserialize<JsonApiResourceObject<SnapshotAttributes>>(JsonApiResults.Options);
        }
        catch (JsonException)
        {
            resource = null;
        }

        if (resource is not { Attributes: { } attributes } || resource.Type != SnapshotDocuments.TYPE)
        {
            return SnapshotAnswer.Failed(SnapshotFailures.MalformedRequest);
        }

        var (request, failure) = SnapshotRequest.From(resource.Id, attributes);
        if (failure != null)
        {
            return SnapshotAnswer.Failed(failure);
        }

        // The caller was let in to the Event of the group: a Snapshot of another is not theirs to send in it.
        return request!.EventId == access.Event.Id
            ? await recorder.RecordAsync(access.User, request, access.Event, cancellationToken)
            : SnapshotAnswer.Failed(SnapshotFailures.EventMismatch);
    }

    static IResult AsResult(SnapshotAnswer answer)
    {
        if (answer.Failure != null)
        {
            return answer.Failure.AsResult();
        }

        var resource = answer.Resource!;
        return JsonApiResults.Resource(
            answer.Status,
            SnapshotDocuments.TYPE,
            resource.Id.ToString(),
            resource,
            answer.Status == StatusCodes.Status201Created ? $"/api/{SnapshotDocuments.TYPE}/{resource.Id}" : null
        );
    }

    /// <summary>The result of an entry of a group: its status and its resource, or its status and its error.</summary>
    static object ResultOf(SnapshotAnswer answer)
    {
        return answer.Failure != null
            ? new { status = answer.Status, errors = new[] { answer.Failure.AsError() } }
            : new
            {
                status = answer.Status,
                data = new
                {
                    type = SnapshotDocuments.TYPE,
                    id = answer.Resource!.Id.ToString(),
                    attributes = answer.Resource,
                },
            };
    }
}
