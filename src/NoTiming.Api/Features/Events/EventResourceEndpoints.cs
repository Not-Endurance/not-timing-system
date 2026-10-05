using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Not.Identity;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Reference;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Core.Models;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Access;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Api.Features.Events;

/// <summary>
/// The Event, the <c>events</c> resource (#628, ADR-0007, ADR-0012, ADR-0008): what an Event is once it has started. It
/// is read by anybody, of every Tenant, because anybody may follow an Event; the named collections
/// <c>/api/events/live</c> and <c>/api/events/historic</c> split the Events by the clock of the host, and every Event
/// carries <c>isLive</c>, the same rule told as an attribute. An Event is Live until the end of its last day and Historic
/// from then on: liveness is derived and never stored. Who runs an Event is not part of what the public sees.
/// <para>
/// A <c>POST</c> names the Setup an Event is started from, as the id of the resource, and starts it (<see cref="EventStarter"/>):
/// the Main Operator's, before the Event starts, and the same Setup again is the Event that was started. A <c>PATCH</c>
/// changes what the Event shows, while it is Live, and a <c>DELETE</c> resets it to its Setup, while it is Live; both are
/// the Main Operator's, and neither can end the Event, because an Event is over when its last day is.
/// </para>
/// </summary>
internal static class EventResourceEndpoints
{
    const string EVENTS = "events";

    public static IEndpointRouteBuilder MapEventResources(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/events",
            (HttpContext context, CrossTenantReads reads, TimeProvider time) => ListAsync(null, context, reads, time)
        );
        app.MapGet(
            "/api/events/live",
            (HttpContext context, CrossTenantReads reads, TimeProvider time) =>
                ListAsync(EventStage.Live, context, reads, time)
        );
        app.MapGet(
            "/api/events/historic",
            (HttpContext context, CrossTenantReads reads, TimeProvider time) =>
                ListAsync(EventStage.Historic, context, reads, time)
        );
        app.MapGet("/api/events/{id}", ReadAsync);
        app.MapPost("/api/events", StartAsync).RequireAuthorization();
        app.MapPatch("/api/events/{id}", ChangeAsync).RequireAuthorization();
        app.MapDelete("/api/events/{id}", ResetAsync).RequireAuthorization();
        return app;
    }

    /// <summary>The attributes of an Event at the instant: what it has, and whether it is Live then.</summary>
    public static Dictionary<string, JsonElement> AttributesOf(EventInformationModel model, DateTimeOffset now)
    {
        var attributes = Members.AttributesOf(model);
        attributes["isLive"] = JsonSerializer.SerializeToElement(
            EventStageRule.Of(true, model.EndDay, now) == EventStage.Live
        );
        return attributes;
    }

    /// <summary>What an Event shows, and what a change of it may name: the rules the Tenant had when it started are shown and not written.</summary>
    public static ReferenceMembers<EventInformationModel> Members { get; } =
        new(["MainOperatorId", "IsActive", "IsDeleted", "DeletedVersion"], ["RegionalRules"]);

    static async Task<IResult> ListAsync(
        EventStage? stage,
        HttpContext context,
        CrossTenantReads reads,
        TimeProvider time
    )
    {
        var query = ReferenceQuery<EventInformationModel>.Read(context.Request, Members.Hidden);
        if (query.Refusal != null)
        {
            return query.Refusal;
        }

        var now = time.GetUtcNow();
        var (rows, unsupported) = await ReferenceEndpoints.ReadPageAsync(
            () => reads.ReadPublicEventsAsync(stage, now, query.Options, query.Skip, query.Take, context.RequestAborted)
        );
        if (unsupported != null)
        {
            return unsupported;
        }

        var page = rows!.Take(query.Size).ToList();
        return JsonApiResults.Collection(
            EVENTS,
            page.Select(x => (x.Id.ToString(), (object)AttributesOf(x, now))),
            links: ReferenceEndpoints.Links(context.Request, query, rows!.Count > query.Size),
            meta: new { page = new { size = query.Size, number = query.Number } },
            itemLink: id => $"/api/{EVENTS}/{id}"
        );
    }

    static async Task<IResult> ReadAsync(string id, HttpContext context, CrossTenantReads reads, TimeProvider time)
    {
        var model = Guid.TryParse(id, out var eventId)
            ? await reads.FindPublicEventAsync(eventId, context.RequestAborted)
            : null;
        return model is null ? JsonApiResults.NotFound() : Resource(model, time.GetUtcNow(), StatusCodes.Status200OK);
    }

    static async Task<IResult> StartAsync(
        HttpContext context,
        UserManager<NIdentityUser> users,
        EventStore events,
        EventStarter starter,
        TenantStore tenantStore,
        TenantCollections tenants,
        CrossTenantReads reads,
        TenancyLog log,
        TimeProvider time
    )
    {
        var read = await JsonApiRequests.ReadAsync<MemberAttributes>(context.Request, EVENTS);
        if (read.Error != null)
        {
            return read.Error;
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        // The Event is its Setup, so the document names the Setup and nothing else.
        if (read.Id == null)
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "id-required",
                "Name the Setup the Event starts from.",
                "The id of the resource is the id of its Setup."
            );
        }

        if (!Guid.TryParse(read.Id, out var id))
        {
            return JsonApiResults.InvalidId();
        }

        if (read.Attributes!.Members is { Count: > 0 } named)
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "unsupported-attribute",
                "An Event is started from its Setup and takes nothing else.",
                string.Join(", ", named.Keys)
            );
        }

        var facts = await events.FindAsync(id, context.RequestAborted);
        if (facts is null)
        {
            return JsonApiResults.NotFound();
        }

        var verdict = AccessPolicy.Decide(
            Capability.ConfigureEvent,
            AccountRoles.CallerOf(user),
            facts.ScopeFor(CallerGrants.None)
        );
        if (!verdict.IsAllowed)
        {
            return AccessResults.Refused(verdict);
        }

        var now = time.GetUtcNow();
        if (facts.Record.IsStarted)
        {
            return await ThatEventAsync(id, reads, now, context.RequestAborted);
        }

        var tenant = await tenantStore.FindAsync(facts.TenantId, context.RequestAborted);
        if (tenant is null)
        {
            return JsonApiResults.Error(
                StatusCodes.Status409Conflict,
                "tenant-not-found",
                "The Event belongs to no Tenant that can start it."
            );
        }

        var setup = await tenants
            .Of<ConfigureEventModel>(TenantOwned.CONFIGURE_EVENTS, facts.TenantId)
            .FindAsync(id, context.RequestAborted);
        if (setup is null)
        {
            return JsonApiResults.NotFound();
        }

        if (ReferenceEndpoints.Invalid<ConfigureEventModel, ConfigureEvent>(setup) is { } invalid)
        {
            return invalid;
        }

        var (plan, refusal) = EventStartPlans.Create(setup.MapToEntity(), tenant.RegionalRules);
        if (refusal != null)
        {
            return refusal;
        }

        if (!await starter.StartAsync(plan!, facts.TenantId, context.RequestAborted))
        {
            return await ThatEventAsync(id, reads, now, context.RequestAborted);
        }

        log.EventStarted(user.Id, id, facts.TenantId);
        return Resource(plan!.EventInformation, now, StatusCodes.Status201Created, $"/api/{EVENTS}/{id}");
    }

    static async Task<IResult> ChangeAsync(
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        EventStore events,
        TenantCollections tenants,
        TimeProvider time
    )
    {
        var read = await JsonApiRequests.ReadAsync<MemberAttributes>(context.Request, EVENTS);
        if (read.Error != null)
        {
            return read.Error;
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        // An Event that has not started is a Setup, which is not an Event, whoever asks.
        var facts = Guid.TryParse(id, out var eventId) ? await events.FindAsync(eventId, context.RequestAborted) : null;
        if (facts is not { Record.IsStarted: true })
        {
            return JsonApiResults.NotFound();
        }

        var verdict = AccessPolicy.Decide(
            Capability.ConfigureEvent,
            AccountRoles.CallerOf(user),
            facts.ScopeFor(CallerGrants.None)
        );
        if (!verdict.IsAllowed)
        {
            return AccessResults.Refused(verdict);
        }

        if (read.Id != null && read.Id != id)
        {
            return JsonApiResults.IdMismatch();
        }

        var collection = tenants.Of<EventInformationModel>(TenantOwned.EVENT_INFORMATIONS, facts.TenantId);
        var row = await collection.FindAsync(eventId, context.RequestAborted);
        if (row is null)
        {
            return JsonApiResults.NotFound();
        }

        var named = new List<PropertyInfo>();
        if (Members.Apply(row, read.Attributes!.Members ?? [], named) is { } malformed)
        {
            return malformed;
        }

        if (ReferenceEndpoints.Invalid<EventInformationModel, EventInformation>(row) is { } invalid)
        {
            return invalid;
        }

        var now = time.GetUtcNow();
        if (named.Any(x => x.Name is nameof(EventInformationModel.StartDay) or nameof(EventInformationModel.EndDay)))
        {
            // The days are whole days in the offset they are given in, as the span of an Event makes them, and the
            // Event is over when its last day is: an Event is not ended by changing its days.
            var span = new EventSpan(row.StartDay, row.EndDay);
            var namedEnd = named.Any(x => x.Name == nameof(EventInformationModel.EndDay));
            if (EventStageRule.Of(true, namedEnd ? span.EndDay : row.EndDay, now) != EventStage.Live)
            {
                return JsonApiResults.Error(
                    StatusCodes.Status422UnprocessableEntity,
                    "invalid-attribute",
                    "A member of the resource is not valid.",
                    "An Event is over when its last day is, and is not ended by changing its days."
                );
            }

            if (named.Any(x => x.Name == nameof(EventInformationModel.StartDay)))
            {
                row.StartDay = span.StartDay;
            }

            if (namedEnd)
            {
                row.EndDay = span.EndDay;
            }
        }

        var update = Members.UpdateOf(row, named);
        if (update.ElementCount > 0 && !await collection.UpdateAsync(eventId, update, context.RequestAborted))
        {
            return JsonApiResults.NotFound();
        }

        return Resource(row, now, StatusCodes.Status200OK);
    }

    static async Task<IResult> ResetAsync(
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        EventStore events,
        EventStarter starter,
        TenancyLog log
    )
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        var facts = Guid.TryParse(id, out var eventId) ? await events.FindAsync(eventId, context.RequestAborted) : null;
        if (facts is null)
        {
            return JsonApiResults.NotFound();
        }

        var verdict = AccessPolicy.Decide(
            Capability.ResetEvent,
            AccountRoles.CallerOf(user),
            facts.ScopeFor(CallerGrants.None)
        );
        if (!verdict.IsAllowed)
        {
            return AccessResults.Refused(verdict);
        }

        await starter.ResetAsync(eventId, facts.TenantId, context.RequestAborted);
        log.EventReset(user.Id, eventId, facts.TenantId);
        return Results.NoContent();
    }

    /// <summary>The Event that was started already, as the answer to starting it again: the first outcome, 200.</summary>
    static async Task<IResult> ThatEventAsync(
        Guid id,
        CrossTenantReads reads,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var model = await reads.FindPublicEventAsync(id, cancellationToken);
        return model is null ? JsonApiResults.NotFound() : Resource(model, now, StatusCodes.Status200OK);
    }

    static IResult Resource(EventInformationModel model, DateTimeOffset now, int status, string? location = null)
    {
        return JsonApiResults.Resource(status, EVENTS, model.Id.ToString(), AttributesOf(model, now), location);
    }
}
