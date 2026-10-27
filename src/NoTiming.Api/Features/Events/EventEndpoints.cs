using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Not.Identity;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Profile;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.Events;

/// <summary>
/// Creating an Event, and what a person may do about one (#643, ADR-0012). Root makes an Event in the Tenant it is acting
/// in and runs it, or assigns Console Operators to run it (<c>/api/console-operators</c>). What a person may do about an
/// Event is told by <c>GET /api/events/{id}/capabilities</c> from the same policy that refuses it. The Event is made with a
/// name and a location and nothing else: it cannot be given a Tenant by what is sent, and the rest of its Setup is the
/// Setup's own routes.
/// </summary>
internal static class EventEndpoints
{
    const string CONFIGURE_EVENTS = "configure-events";
    const string CAPABILITIES = "capabilities";

    public static IEndpointRouteBuilder MapEvents(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/configure-events", Create);
        app.MapGet("/api/events/{id}/capabilities", ReadCapabilities);
        return app;
    }

    // The Event is made in the current Tenant of the account: nothing that is sent names another.
    [Requires(Capability.CreateEvent, PlaceIn.CurrentTenant)]
    static async Task<IResult> Create(
        JsonApiBody<NewEventAttributes> body,
        AccessContext access,
        TenantCollections collections,
        EventStore events,
        TenancyLog log,
        CancellationToken cancellationToken
    )
    {
        var tenantId = access.Tenant.Id;
        var attributes = body.Attributes;
        var requested = body.Id is null ? (Guid?)null : Guid.Parse(body.Id);
        var (id, created) = await events.CreateAsync(
            tenantId,
            requested,
            attributes.NameText,
            attributes.LocationText,
            attributes.FeiShowIdText,
            cancellationToken
        );
        if (!created)
        {
            // The same id again is the Event that was made. An Event of another Tenant with it is not told about.
            var existing = await collections
                .Of<ConfigureEventModel>(TenantOwned.CONFIGURE_EVENTS, tenantId)
                .FindAsync(id, cancellationToken);
            return existing is null
                ? JsonApiResults.IdTaken()
                : JsonApiResults.Resource(
                    StatusCodes.Status200OK,
                    CONFIGURE_EVENTS,
                    id.ToString(),
                    SetupDocument.Members.AttributesOf(existing)
                );
        }

        log.EventCreated(access.User.Id, id, tenantId);
        return JsonApiResults.Resource(
            StatusCodes.Status201Created,
            CONFIGURE_EVENTS,
            id.ToString(),
            new
            {
                name = attributes.NameText,
                location = attributes.LocationText,
                feiShowId = attributes.FeiShowIdText,
                tenantId,
            },
            $"/api/{CONFIGURE_EVENTS}/{id}"
        );
    }

    // What a person may do about an Event is the policy's own answer, so that a screen and the server cannot differ.
    [Requires(Capability.SignedIn, PlaceIn.EventRoute)]
    static async Task<IResult> ReadCapabilities(
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
