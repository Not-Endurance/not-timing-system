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
/// a Console Operator's or Root's, before the Event starts, and the same Setup again is the Event that was started. A
/// <c>PATCH</c> changes what the Event shows, while it is Live, and a <c>DELETE</c> resets it to its Setup, while it is Live;
/// both are theirs, and neither can end the Event, because an Event is over when its last day is.
/// </para>
/// </summary>
internal static class EventEndpoints
{
    const string EVENTS = "events";
    const string CAPABILITIES = "capabilities";

    public static IEndpointRouteBuilder MapEvents(this IEndpointRouteBuilder app)
    {
        app.MapNamedGet(
            "/api/events",
            (HttpContext context, CrossTenantReads reads, TimeProvider time) => ListAsync(null, context, reads, time)
        );
        app.MapNamedGet(
            "/api/events/live",
            (HttpContext context, CrossTenantReads reads, TimeProvider time) =>
                ListAsync(EventStage.Live, context, reads, time)
        );
        app.MapNamedGet(
            "/api/events/historic",
            (HttpContext context, CrossTenantReads reads, TimeProvider time) =>
                ListAsync(EventStage.Historic, context, reads, time)
        );
        app.MapGet("/api/events/{id}", GetEventsById);
        app.MapPost("/api/events", PostEvents);
        app.MapPatch("/api/events/{id}", PatchEventsById);
        app.MapDelete("/api/events/{id}", DeleteEventsById);
        app.MapGet("/api/events/{id}/capabilities", GetEventsByIdCapabilities);
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
        new(["IsDeleted", "DeletedVersion"], ["RegionalRules"]);

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
        var rows = await reads.ReadPublicEventsAsync(
            stage,
            now,
            query.Options,
            query.Skip,
            query.Take,
            context.RequestAborted
        );
        return JsonApiResults.Page(
            EVENTS,
            rows.Take(query.Size).Select(x => (x.Id.ToString(), (object)AttributesOf(x, now))),
            context.Request,
            query.Size,
            query.Number,
            rows.Count > query.Size,
            itemLink: id => $"/api/{EVENTS}/{id}"
        );
    }

    static async Task<IResult> GetEventsById(string id, HttpContext context, CrossTenantReads reads, TimeProvider time)
    {
        var model = Guid.TryParse(id, out var eventId)
            ? await reads.FindPublicEventAsync(eventId, context.RequestAborted)
            : null;
        return model is null ? JsonApiResults.NotFound() : Resource(model, time.GetUtcNow(), StatusCodes.Status200OK);
    }

    // The Event is its Setup, so the document names the Setup and nothing else.
    [Requires(Capability.ConfigureEvent, PlaceIn.EventInDocument)]
    static async Task<IResult> PostEvents(
        JsonApiBody<EventStart> body,
        AccessContext access,
        EventStarter starter,
        TenantStore tenantStore,
        TenantCollections tenants,
        CrossTenantReads reads,
        TenancyLog log,
        TimeProvider time,
        CancellationToken cancellationToken
    )
    {
        var facts = access.Event;
        var id = facts.Id;
        var now = time.GetUtcNow();
        if (facts.Record.IsStarted)
        {
            return await ThatEventAsync(id, reads, now, cancellationToken);
        }

        var tenant = await tenantStore.FindAsync(facts.TenantId, cancellationToken);
        if (tenant is null)
        {
            return EventsFailures.TenantNotFound.AsResult();
        }

        var setup = await tenants
            .Of<ConfigureEventModel>(TenantOwned.CONFIGURE_EVENTS, facts.TenantId)
            .FindAsync(id, cancellationToken);
        if (setup is null)
        {
            return JsonApiResults.NotFound();
        }

        if (ReferenceRows.Invalid<ConfigureEventModel, ConfigureEvent>(setup) is { } invalid)
        {
            return invalid;
        }

        var (plan, refusal) = EventStartPlans.Create(setup.MapToEntity(), tenant.RegionalRules);
        if (refusal != null)
        {
            return refusal;
        }

        if (!await starter.StartAsync(plan!, facts.TenantId, cancellationToken))
        {
            return await ThatEventAsync(id, reads, now, cancellationToken);
        }

        log.EventStarted(access.User.Id, id, facts.TenantId);
        return Resource(plan!.EventInformation, now, StatusCodes.Status201Created, $"/api/{EVENTS}/{id}");
    }

    [Requires(Capability.ConfigureEvent, PlaceIn.StartedEventRoute)]
    static async Task<IResult> PatchEventsById(
        JsonApiBody<EventChange> body,
        AccessContext access,
        TenantCollections tenants,
        TimeProvider time,
        CancellationToken cancellationToken
    )
    {
        var facts = access.Event;
        var eventId = facts.Id;
        var collection = tenants.Of<EventInformationModel>(TenantOwned.EVENT_INFORMATIONS, facts.TenantId);
        var row = await collection.FindAsync(eventId, cancellationToken);
        if (row is null)
        {
            return JsonApiResults.NotFound();
        }

        var named = new List<PropertyInfo>();
        if (Members.Apply(row, body.Attributes.Members ?? [], named) is { } malformed)
        {
            return malformed;
        }

        if (ReferenceRows.Invalid<EventInformationModel, EventInformation>(row) is { } invalid)
        {
            return invalid;
        }

        var now = time.GetUtcNow();
        if (named.Any(x => x.Name is nameof(EventInformationModel.StartDay) or nameof(EventInformationModel.EndDay)))
        {
            // The days are whole days in the offset they are given in, as the span of an Event makes them, and the
            // Event is over when its last day is: an Event is not ended by changing its days.
            var span = new EventSpan(row.StartDay, row.EndDay);
            if (EventStageRule.Of(true, span.EndDay, now) != EventStage.Live)
            {
                return EventsFailures.InvalidAttribute.AsResult();
            }

            if (span.StartDay > span.EndDay)
            {
                return EventsFailures.EventInvalidAttribute.AsResult();
            }

            if (named.Any(x => x.Name == nameof(EventInformationModel.StartDay)))
            {
                row.StartDay = span.StartDay;
            }

            if (named.Any(x => x.Name == nameof(EventInformationModel.EndDay)))
            {
                row.EndDay = span.EndDay;
            }
        }

        var update = Members.UpdateOf(row, named);
        if (update.ElementCount > 0 && !await collection.UpdateAsync(eventId, update, cancellationToken))
        {
            return JsonApiResults.NotFound();
        }

        return Resource(row, now, StatusCodes.Status200OK);
    }

    [Requires(Capability.RunEvent, PlaceIn.EventRoute)]
    static async Task<IResult> DeleteEventsById(
        AccessContext access,
        EventStarter starter,
        TenancyLog log,
        CancellationToken cancellationToken
    )
    {
        var facts = access.Event;
        await starter.ResetAsync(facts.Id, facts.TenantId, cancellationToken);
        log.EventReset(access.User.Id, facts.Id, facts.TenantId);
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

    // What a person may do about an Event is the policy's own answer, so that a screen and the server cannot differ.
    [Requires(Capability.SignedIn, PlaceIn.EventRoute)]
    static async Task<IResult> GetEventsByIdCapabilities(
        AccessContext access,
        TenantStore tenants,
        StandingReader standings,
        CancellationToken cancellationToken
    )
    {
        var facts = access.Event;
        var standing = await standings.AtEventAsync(access.User, facts, cancellationToken);
        var inTheTenant = await standings.AtTenantAsync(access.User, facts.TenantId, cancellationToken);
        var tenant = PlaceState.OfTenant(await tenants.IsOperationalAsync(facts.TenantId, cancellationToken));
        return JsonApiResults.Resource(
            StatusCodes.Status200OK,
            CAPABILITIES,
            facts.Id.ToString(),
            new
            {
                stage = facts.Stage.ToString().ToLowerInvariant(),
                level = AccessLevelText.Of(standing.Level),
                canSnapshot = AccessPolicy.Decide(Capability.SendSnapshot, standing, facts.State).IsAllowed,
                canConfigure = AccessPolicy.Decide(Capability.ConfigureEvent, standing, facts.State).IsAllowed,
                canEditSetup = AccessPolicy.Decide(Capability.EditSetup, standing, facts.State).IsAllowed,
                canRun = AccessPolicy.Decide(Capability.RunEvent, standing, facts.State).IsAllowed,
                canAssignConsoleOperators = AccessPolicy
                    .Decide(Capability.AssignConsoleOperators, standing, facts.State)
                    .IsAllowed,
                canCreateEvents = AccessPolicy.Decide(Capability.CreateEvent, inTheTenant, tenant).IsAllowed,
            }
        );
    }
}
