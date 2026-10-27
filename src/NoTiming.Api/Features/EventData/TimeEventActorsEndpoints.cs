using MongoDB.Bson;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.EventData;

/// <summary>
/// The people who recorded a time of an Event, named (#619, ADR-0005): <c>GET /api/time-event-actors?filter=eventId eq {id}</c>
/// answers one resource for each account that is the actor of a time event of the Event, whose id is the account's and whose
/// only attribute is its display name. Time events carry the id of the account that recorded them and never a name, so this is
/// how the timeline of the Console says who it was. It is for the people who may see how times were recorded and nobody else
/// (403 <c>not-allowed</c>, decided by the endpoint filter), it names only accounts that recorded a time of this Event, and it
/// takes the Event, the <c>sort</c> by the display name and the pages of every list, and nothing else. An account that is gone
/// is not named, and the timeline says it does not know it.
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
        app.MapGet("/api/" + TYPE, ReadAsync);
        return app;
    }

    [Requires(Capability.SeeTimeEvents, PlaceIn.EventInFilter)]
    static async Task<IResult> ReadAsync(
        [ListOf(DISPLAY_NAME, EVENT_ID)] ListQuery query,
        HttpRequest request,
        AccessContext access,
        TenantCollections tenants,
        AccountSearch accounts,
        CancellationToken cancellationToken
    )
    {
        var facts = access.Event;
        var recorded = await tenants
            .Of(TenantOwned.EVENT_PARTICIPATIONS, facts.TenantId)
            .DistinctAsync(ACTOR_ID, new BsonDocument(EVENT_ID_FIELD, BsonGuids.Binary(facts.Id)), cancellationToken);
        var (actors, hasMore) = await accounts.NamesOfAsync(
            [.. recorded.OfType<BsonBinaryData>().Select(x => x.ToGuid(GuidRepresentation.Standard)).Distinct()],
            query.Descending,
            query.Skip,
            query.Size,
            cancellationToken
        );
        return JsonApiResults.Page(
            TYPE,
            actors.Select(x => (x.Id.ToString(), (object)new { displayName = x.DisplayName })),
            request,
            query.Size,
            query.Number,
            hasMore
        );
    }
}
