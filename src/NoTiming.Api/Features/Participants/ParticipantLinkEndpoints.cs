using System.Globalization;
using Microsoft.Net.Http.Headers;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Account;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Features.Participants;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.Participants;

/// <summary>
/// The links of a signed-in person to the registries (#638, ADR-0010): <c>/api/athletes/{id}/link</c> links the caller as that
/// Athlete (a bookmark, not a claim: nothing is granted by it), and <c>/api/horses/{id}/link</c> adds that Horse to the caller's
/// Horses, at most a hundred. <c>POST</c> makes the link (201 and its <c>Location</c>, or 200 when the caller had it already; a
/// caller has one Athlete, so linking another moves the link), <c>GET</c> reads it and <c>DELETE</c> takes it away (204). The
/// routes name no account, so there is no way to reach another's links, and a link is a <c>participant-links</c> resource whose
/// id is the Participant's. The Participant has to be a row of the registry of any Tenant (404 <c>not-found</c> otherwise); a link
/// to a row that has been deleted since stays, and can be taken away. What a person is linked to is told by their profile.
/// </summary>
internal static class ParticipantLinkEndpoints
{
    const string PARTICIPANT_LINKS = "participant-links";
    public const int MAX_HORSES = 100;

    public static IEndpointRouteBuilder MapParticipantLinks(this IEndpointRouteBuilder app)
    {
        Map(app, ParticipantKind.Athlete);
        Map(app, ParticipantKind.Horse);
        return app;
    }

    static void Map(IEndpointRouteBuilder app, ParticipantKind kind)
    {
        var path = $"/api/{RouteOf(kind)}/{{id}}/link";
        app.MapGet(
            path,
            [Requires(Capability.SignedIn)]
            (
                string id,
                HttpContext context,
                AccessContext access,
                ParticipantLinkStore store,
                CrossTenantReads reads,
                AccountText text
            ) => ReadAsync(kind, id, context, access, store, reads, text)
        );
        app.MapPost(
            path,
            [Requires(Capability.SignedIn)]
            (
                string id,
                HttpContext context,
                AccessContext access,
                ParticipantLinkStore store,
                CrossTenantReads reads,
                AccountText text
            ) => LinkAsync(kind, id, context, access, store, reads, text)
        );
        app.MapDelete(
            path,
            [Requires(Capability.SignedIn)]
            (string id, AccessContext access, ParticipantLinkStore store, CancellationToken cancellationToken) =>
                UnlinkAsync(kind, id, access, store, cancellationToken)
        );
    }

    static async Task<IResult> ReadAsync(
        ParticipantKind kind,
        string id,
        HttpContext context,
        AccessContext access,
        ParticipantLinkStore store,
        CrossTenantReads reads,
        AccountText text
    )
    {
        if (!Guid.TryParse(id, out var participantId))
        {
            return JsonApiResults.NotFound();
        }

        var links = await store.FindAsync(access.User.Id, context.RequestAborted);
        var isLinked =
            kind == ParticipantKind.Athlete ? links.AthleteId == participantId : links.HorseIds.Contains(participantId);
        var participant = isLinked ? await FindAsync(kind, participantId, reads, context.RequestAborted) : null;
        return participant is null
            ? JsonApiResults.NotFound()
            : Resource(context, text, participant, StatusCodes.Status200OK);
    }

    static async Task<IResult> LinkAsync(
        ParticipantKind kind,
        string id,
        HttpContext context,
        AccessContext access,
        ParticipantLinkStore store,
        CrossTenantReads reads,
        AccountText text
    )
    {
        var participant = Guid.TryParse(id, out var participantId)
            ? await FindAsync(kind, participantId, reads, context.RequestAborted)
            : null;
        if (participant is null)
        {
            return JsonApiResults.NotFound();
        }

        bool created;
        if (kind == ParticipantKind.Athlete)
        {
            created = await store.LinkAthleteAsync(access.User.Id, participantId, context.RequestAborted);
        }
        else
        {
            var linking = await store.LinkHorseAsync(access.User.Id, participantId, MAX_HORSES, context.RequestAborted);
            if (linking == HorseLinking.LimitReached)
            {
                return ParticipantFailures.HorseLimitReached(MAX_HORSES).AsResult();
            }

            created = linking == HorseLinking.Linked;
        }

        return Resource(
            context,
            text,
            participant,
            created ? StatusCodes.Status201Created : StatusCodes.Status200OK,
            created ? $"/api/{RouteOf(kind)}/{participantId}/link" : null
        );
    }

    static async Task<IResult> UnlinkAsync(
        ParticipantKind kind,
        string id,
        AccessContext access,
        ParticipantLinkStore store,
        CancellationToken cancellationToken
    )
    {
        if (!Guid.TryParse(id, out var participantId))
        {
            return JsonApiResults.NotFound();
        }

        var removed =
            kind == ParticipantKind.Athlete
                ? await store.UnlinkAthleteAsync(access.User.Id, participantId, cancellationToken)
                : await store.UnlinkHorseAsync(access.User.Id, participantId, cancellationToken);
        return removed ? Results.NoContent() : JsonApiResults.NotFound();
    }

    /// <summary>The collection of the registry of a kind, which is where its links are.</summary>
    static string RouteOf(ParticipantKind kind)
    {
        return kind == ParticipantKind.Athlete ? "athletes" : "horses";
    }

    static async Task<Participant?> FindAsync(
        ParticipantKind kind,
        Guid participantId,
        CrossTenantReads reads,
        CancellationToken cancellationToken
    )
    {
        var found = await reads.FindParticipantsAsync(Standing.Account, kind, [participantId], cancellationToken);
        return found.FirstOrDefault();
    }

    static IResult Resource(
        HttpContext context,
        AccountText text,
        Participant participant,
        int status,
        string? location = null
    )
    {
        var named = participant.NameIn(null, CultureInfo.GetCultureInfo(text.Resolve(context.Request)));

        // The name is told in the language of the request.
        context.Response.Headers.Append(HeaderNames.Vary, HeaderNames.AcceptLanguage);
        return JsonApiResults.Resource(
            status,
            PARTICIPANT_LINKS,
            participant.Id.ToString(),
            new { kind = named.Kind, displayName = named.DisplayName },
            location
        );
    }
}
