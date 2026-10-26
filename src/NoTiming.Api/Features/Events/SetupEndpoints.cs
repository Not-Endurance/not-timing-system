using Microsoft.AspNetCore.Identity;
using Not.Identity;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Branding;
using NoTiming.Api.Features.Reference;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Access;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Api.Features.Events;

/// <summary>
/// The Setup of an Event, the <c>configure-events</c> resource (#603, ADR-0012): what an Event is configured with until it
/// starts. Root makes the Event (<see cref="EventEndpoints"/>); this reads, changes and deletes it. It is read by a Console
/// Operator of the Event and by Root; changed by them until the Event starts, and then by nobody, with 409 and the stage the
/// Event is at, because from the start the Console works on the copies the Event made of it; deleted, with the grants of the
/// Event, by Root before it starts. The Tenant of the Event is shown and cannot be written. The members and the filter, the
/// sort and the pages of a list are those of every family of reference data (<see cref="ReferenceEndpoints"/>).
/// </summary>
internal static class SetupEndpoints
{
    const string CONFIGURE_EVENTS = "configure-events";

    public static IEndpointRouteBuilder MapSetups(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/configure-events", ListAsync).RequireAuthorization();
        app.MapGet("/api/configure-events/{id}", ReadAsync).RequireAuthorization();
        app.MapPatch("/api/configure-events/{id}", ChangeAsync).RequireAuthorization();
        app.MapDelete("/api/configure-events/{id}", RemoveAsync).RequireAuthorization();
        return app;
    }

    /// <summary>The members the Setup is shown with: its own, and the Tenant, which only the server sets.</summary>
    public static ReferenceMembers<ConfigureEventModel> Members { get; } = new([], []);

    static async Task<IResult> ListAsync(
        HttpContext context,
        UserManager<NIdentityUser> users,
        TenantCollections tenants,
        EventGrantStore grants
    )
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        var query = ReferenceQuery<ConfigureEventModel>.Read(context.Request, Members.Hidden);
        if (query.Refusal != null)
        {
            return query.Refusal;
        }

        // Root sees the Setups of the Tenant it acts in, and anybody else the ones of the Events it is a Console Operator of:
        // the Setup is for the people who run the Event and the Tenant that holds it.
        var tenantId = AccountRoles.CurrentTenantOf(user);
        var roles = AccountRoles.RolesOf(user);
        var all = roles.IsDeveloper || (tenantId != null && roles.IsRootAt(tenantId));
        Guid[]? running = all ? null : [.. await grants.EventsRunByAsync(tenantId, user.Id, context.RequestAborted)];
        var (rows, unsupported) = await ReferenceEndpoints.ReadPageAsync(
            () =>
                tenants
                    .Of<ConfigureEventModel>(TenantOwned.CONFIGURE_EVENTS, tenantId)
                    .ReadAsync(
                        running == null ? null : x => running.Contains(x.Id),
                        query.Options,
                        query.Skip,
                        query.Take,
                        context.RequestAborted
                    )
        );
        if (unsupported != null)
        {
            return unsupported;
        }

        var page = rows!.Take(query.Size).ToList();

        return JsonApiResults.Collection(
            CONFIGURE_EVENTS,
            page.Select(x => (x.Id.ToString(), (object)Members.AttributesOf(x))),
            links: ReferenceEndpoints.Links(context.Request, query, rows!.Count > query.Size),
            meta: new { page = new { size = query.Size, number = query.Number } }
        );
    }

    static async Task<IResult> ReadAsync(
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        StandingReader standing,
        EventStore events,
        TenantCollections tenants
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

        var verdict = await standing.DecideAtEventAsync(
            Capability.ReadConfiguration,
            user,
            facts,
            context.RequestAborted
        );
        if (!verdict.IsAllowed)
        {
            return AccessResults.Refused(verdict);
        }

        var setup = await Open(tenants, facts).FindAsync(eventId, context.RequestAborted);
        return setup is null ? JsonApiResults.NotFound() : Resource(setup, facts, StatusCodes.Status200OK);
    }

    static async Task<IResult> ChangeAsync(
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        StandingReader standing,
        EventStore events,
        TenantCollections tenants
    )
    {
        var read = await JsonApiRequests.ReadAsync<MemberAttributes>(context.Request, CONFIGURE_EVENTS);
        if (read.Error != null)
        {
            return read.Error;
        }

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

        var verdict = await standing.DecideAtEventAsync(Capability.EditSetup, user, facts, context.RequestAborted);
        if (!verdict.IsAllowed)
        {
            return AccessResults.Refused(verdict);
        }

        if (read.Id != null && read.Id != id)
        {
            return JsonApiResults.IdMismatch();
        }

        var collection = Open(tenants, facts);
        var setup = await collection.FindAsync(eventId, context.RequestAborted);
        if (setup is null)
        {
            return JsonApiResults.NotFound();
        }

        var failed = await ReferenceEndpoints.ChangeRowAsync<ConfigureEventModel, ConfigureEvent>(
            Members,
            collection,
            setup,
            read.Attributes!.Members ?? [],
            context.RequestAborted
        );
        return failed ?? Resource(setup, facts, StatusCodes.Status200OK);
    }

    static async Task<IResult> RemoveAsync(
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        StandingReader standing,
        EventStore events,
        TenantCollections tenants,
        EventGrantStore grants,
        BrandingStore branding,
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

        var verdict = await standing.DecideAtEventAsync(Capability.DeleteEvent, user, facts, context.RequestAborted);
        if (!verdict.IsAllowed)
        {
            return AccessResults.Refused(verdict);
        }

        if (!await Open(tenants, facts).DeleteAsync(eventId, context.RequestAborted))
        {
            return JsonApiResults.NotFound();
        }

        // What was given on the Event goes with it, so that no grant is left waiting for an Event that is not there.
        await grants.RemoveAllOfAsync(facts.Record, context.RequestAborted);
        await branding.RemoveOfEventAsync(facts.TenantId, eventId, context.RequestAborted); // and the logos it put in place of the Tenant's
        log.EventDeleted(user.Id, eventId, facts.TenantId);
        return Results.NoContent();
    }

    static TypedTenantCollection<ConfigureEventModel> Open(TenantCollections tenants, EventFacts facts)
    {
        return tenants.Of<ConfigureEventModel>(TenantOwned.CONFIGURE_EVENTS, facts.TenantId);
    }

    static IResult Resource(ConfigureEventModel setup, EventFacts facts, int status)
    {
        return JsonApiResults.Resource(status, CONFIGURE_EVENTS, setup.Id.ToString(), Members.AttributesOf(setup));
    }
}
