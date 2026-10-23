using Microsoft.AspNetCore.Identity;
using MongoDB.Bson;
using Not.Identity;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Events;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.EventData;

/// <summary>
/// The people who recorded a time of an Event, named (#619, ADR-0005): <c>GET /api/time-event-actors?filter=eventId eq {id}</c>
/// answers one resource for each account that is the actor of a time event of the Event, whose id is the account's and whose
/// only attribute is its display name. Time events carry the id of the account that recorded them and never a name, so this is
/// how the timeline of the Console says who it was. It is for the people who may see how times were recorded and nobody else
/// (403 <c>not-allowed</c>), it names only accounts that recorded a time of this Event, and it takes the Event, the
/// <c>sort</c> by the display name and the pages of every list, and nothing else. An account that is gone is not named,
/// and the timeline says it does not know it.
/// </summary>
internal static class TimeEventActorsEndpoints
{
    const string TYPE = "time-event-actors";
    const string EVENT_ID = "eventId";
    const string DISPLAY_NAME = "displayName";
    const string EVENT_ID_FIELD = "EventId";
    const string ACTOR_ID = "Phases.Events.ActorId";

    public static IEndpointRouteBuilder MapTimeEventActors(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/" + TYPE,
                (
                    HttpContext context,
                    UserManager<NIdentityUser> users,
                    EventStore events,
                    TenantCollections tenants,
                    TimeEventViewers viewers,
                    AccountSearch accounts
                ) => ReadAsync(context, users, events, tenants, viewers, accounts)
            )
            .RequireAuthorization();
        return app;
    }

    static async Task<IResult> ReadAsync(
        HttpContext context,
        UserManager<NIdentityUser> users,
        EventStore events,
        TenantCollections tenants,
        TimeEventViewers viewers,
        AccountSearch accounts
    )
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        var query = JsonApiQuery.ReadList(context.Request, DISPLAY_NAME, EVENT_ID);
        if (query.Refusal != null)
        {
            return query.Refusal;
        }

        if (!query.EqualTo.TryGetValue(EVENT_ID, out var named) || !Guid.TryParse(named, out var eventId))
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "event-required",
                "Name the Event the people recorded times of.",
                "Send filter=eventId eq <id> and nothing else."
            );
        }

        var facts = await events.FindAsync(eventId, context.RequestAborted);
        if (facts is null)
        {
            return JsonApiResults.NotFound();
        }

        var verdict = await viewers.DecideAsync(user, facts, context.RequestAborted);
        if (!verdict.IsAllowed)
        {
            return AccessResults.Refused(verdict);
        }

        var recorded = await tenants
            .Of(TenantOwned.EVENT_PARTICIPATIONS, facts.TenantId)
            .DistinctAsync(
                ACTOR_ID,
                new BsonDocument(EVENT_ID_FIELD, BsonGuids.Binary(eventId)),
                context.RequestAborted
            );
        var (actors, hasMore) = await accounts.NamesOfAsync(
            [.. recorded.OfType<BsonBinaryData>().Select(x => x.ToGuid(GuidRepresentation.Standard)).Distinct()],
            query.Descending,
            query.Skip,
            query.Size,
            context.RequestAborted
        );
        return JsonApiResults.Collection(
            TYPE,
            actors.Select(x => (x.Id.ToString(), (object)new { displayName = x.DisplayName })),
            links: JsonApiPaging.Links(context.Request, query.Number, hasMore),
            meta: JsonApiPaging.Meta(query.Size, query.Number)
        );
    }
}
