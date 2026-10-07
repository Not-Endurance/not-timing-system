using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Not.Identity;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Snapshots;

/// <summary>
/// The routes of the Snapshots (#644, ADR-0013, the rest-api skill). <c>POST /api/snapshots</c> sends one: the id its device
/// made for it, the Event, the start number, the kind and the time it captured, and the answer is the time event the server
/// recorded, with its outcome: 201 whatever the outcome, because the event is what was made, and 200 with the first answer
/// for an id that was sent before. <c>POST /api/snapshots/actions/send-group</c> sends a list of them, processed in order,
/// each answered in its own result and none stopping the ones after it. <c>PATCH /api/snapshots/{id}</c> is an Update of a
/// Snapshot that was sent: the time it should have had, an absolute value, and the answer says what changed or why not.
/// Every route needs a signed-in caller, and the policy decides on each Snapshot whether the caller may send one to its
/// Event now.
/// </summary>
internal static class SnapshotEndpoints
{
    public const string TYPE = "snapshots";
    public const int MAX_IN_A_GROUP = 100;

    public static IEndpointRouteBuilder MapSnapshots(this IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/api/snapshots",
                (HttpContext context, UserManager<NIdentityUser> users, SnapshotRecorder recorder) =>
                    SendAsync(context, users, recorder)
            )
            .RequireAuthorization();
        app.MapPost(
                "/api/snapshots/actions/send-group",
                (HttpContext context, UserManager<NIdentityUser> users, SnapshotRecorder recorder) =>
                    SendGroupAsync(context, users, recorder)
            )
            .RequireAuthorization();
        app.MapPatch(
                "/api/snapshots/{id}",
                (string id, HttpContext context, UserManager<NIdentityUser> users, SnapshotRecorder recorder) =>
                    UpdateAsync(id, context, users, recorder)
            )
            .RequireAuthorization();
        return app;
    }

    static async Task<IResult> SendAsync(
        HttpContext context,
        UserManager<NIdentityUser> users,
        SnapshotRecorder recorder
    )
    {
        var read = await JsonApiRequests.ReadAsync<SnapshotAttributes>(context.Request, TYPE);
        if (read.Error != null)
        {
            return read.Error;
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        var (request, failure) = SnapshotRequest.From(read.Id, read.Attributes!);
        if (failure != null)
        {
            return failure.AsResult();
        }

        return AsResult(await recorder.RecordAsync(user, request!, context.RequestAborted));
    }

    static async Task<IResult> SendGroupAsync(
        HttpContext context,
        UserManager<NIdentityUser> users,
        SnapshotRecorder recorder
    )
    {
        var (entries, error) = await JsonApiRequests.ReadListAsync(context.Request);
        if (error != null)
        {
            return error;
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        if (entries!.Count > MAX_IN_A_GROUP)
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "too-many-snapshots",
                "A group has too many Snapshots.",
                $"Send at most {MAX_IN_A_GROUP} in one request."
            );
        }

        var results = new List<object>();
        foreach (var entry in entries)
        {
            results.Add(ResultOf(await SendEntryAsync(entry, user, recorder, context.RequestAborted)));
        }

        return Results.Json(
            new { meta = new { results } },
            JsonApiResults.Options,
            JsonApiResults.MEDIA_TYPE,
            StatusCodes.Status200OK
        );
    }

    static async Task<IResult> UpdateAsync(
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        SnapshotRecorder recorder
    )
    {
        var read = await JsonApiRequests.ReadAsync<SnapshotUpdateAttributes>(context.Request, TYPE);
        if (read.Error != null)
        {
            return read.Error;
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        if (!Guid.TryParse(id, out var snapshotId))
        {
            return JsonApiResults.NotFound();
        }

        if (read.Id != null && read.Id != id)
        {
            return JsonApiResults.IdMismatch();
        }

        if (SnapshotRequest.Unsupported(read.Attributes!.Others) is { } unsupported)
        {
            return unsupported.AsResult();
        }

        if (read.Attributes.Time is not { } time)
        {
            return SnapshotRequest
                .Invalid("Say the time the Snapshot should have had: an instant with its offset.")
                .AsResult();
        }

        return AsResult(await recorder.UpdateAsync(user, snapshotId, time, context.RequestAborted));
    }

    /// <summary>One entry of a group: read as a Snapshot and sent, or the failure that says what is wrong with it.</summary>
    static async Task<SnapshotAnswer> SendEntryAsync(
        JsonElement entry,
        NIdentityUser user,
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

        if (resource is not { Attributes: { } attributes } || resource.Type != TYPE)
        {
            return SnapshotAnswer.Failed(
                new JsonApiFailure(
                    StatusCodes.Status400BadRequest,
                    "malformed-request",
                    "The request is malformed.",
                    $"Send each Snapshot as a resource of type '{TYPE}' with attributes."
                )
            );
        }

        var (request, failure) = SnapshotRequest.From(resource.Id, attributes);
        return failure != null
            ? SnapshotAnswer.Failed(failure)
            : await recorder.RecordAsync(user, request!, cancellationToken);
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
            TYPE,
            resource.Id.ToString(),
            resource,
            answer.Status == StatusCodes.Status201Created ? $"/api/{TYPE}/{resource.Id}" : null
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
                    type = TYPE,
                    id = answer.Resource!.Id.ToString(),
                    attributes = answer.Resource,
                },
            };
    }
}
