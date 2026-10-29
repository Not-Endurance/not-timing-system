using Microsoft.AspNetCore.Identity;
using Not.Identity;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Reference;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Access;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Api.Features.Events;

/// <summary>
/// The Setup of an Event, the <c>configure-events</c> resource (#603, ADR-0012): what an Event is configured with until it
/// starts. A Tenant Root makes the Event (<see cref="EventEndpoints"/>); this reads, changes and deletes it. It is read by
/// the Main Operator, a Tenant Root of the Tenant that holds it and the Developer; changed by its Main Operator, or by the
/// Developer, until the Event starts, and then by nobody, with 409 and the stage the Event is at, because from the start the
/// Console works on the copies the Event made of it; deleted, with the grants of the Event, by a Tenant Root or the
/// Developer before it starts. The Tenant and the Main Operator of the Event are shown and cannot be written: what the
/// Event has now is the Core document's once it has started, so that is what is shown. The members and the filter, the
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

    /// <summary>The members the Setup is shown with: its own, and the Main Operator, which only the server sets.</summary>
    public static ReferenceMembers<ConfigureEventModel> Members { get; } =
        new([], [nameof(ConfigureEventModel.MainOperatorId)]);

    static async Task<IResult> ListAsync(
        HttpContext context,
        UserManager<NIdentityUser> users,
        TenantCollections tenants,
        EventStore events
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

        // A Tenant Root sees the Setups of its Tenant, the Developer those of the Tenant it acts in, and anybody else the
        // ones of the Events it runs now: the Setup is for the people who run the Event and the Tenant that holds it.
        var tenantId = AccountRoles.CurrentTenantOf(user);
        var caller = AccountRoles.CallerOf(user);
        var all = caller.IsDeveloper || (tenantId != null && caller.TenantRootIn.Contains(tenantId));
        Guid[]? running = all ? null : [.. await events.RunByAsync(tenantId, user.Id, context.RequestAborted)];
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

        // An Event that has started has the Main Operator its Core says, which a hand-over changes.
        var started = await events.StartedOperatorsAsync(tenantId, [.. page.Select(x => x.Id)], context.RequestAborted);
        foreach (var setup in page.Where(x => started.ContainsKey(x.Id)))
        {
            setup.MainOperatorId = started[setup.Id];
        }

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

        var verdict = AccessPolicy.Decide(
            Capability.ReadSetup,
            AccountRoles.CallerOf(user),
            facts.ScopeFor(CallerGrants.None)
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

        var verdict = AccessPolicy.Decide(
            Capability.EditSetup,
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
        EventStore events,
        TenantCollections tenants,
        EventGrantStore grants,
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
            Capability.DeleteEvent,
            AccountRoles.CallerOf(user),
            facts.ScopeFor(CallerGrants.None)
        );
        if (!verdict.IsAllowed)
        {
            return AccessResults.Refused(verdict);
        }

        if (!await Open(tenants, facts).DeleteAsync(eventId, context.RequestAborted))
        {
            return JsonApiResults.NotFound();
        }

        // What the Main Operator gave on the Event goes with it, so that no grant is left waiting for an Event that is not there.
        await grants.RemoveAllOfAsync(facts.Record, context.RequestAborted);
        log.EventDeleted(user.Id, eventId, facts.TenantId);
        return Results.NoContent();
    }

    static TypedTenantCollection<ConfigureEventModel> Open(TenantCollections tenants, EventFacts facts)
    {
        return tenants.Of<ConfigureEventModel>(TenantOwned.CONFIGURE_EVENTS, facts.TenantId);
    }

    static IResult Resource(ConfigureEventModel setup, EventFacts facts, int status)
    {
        setup.MainOperatorId = facts.Record.MainOperatorId;
        return JsonApiResults.Resource(status, CONFIGURE_EVENTS, setup.Id.ToString(), Members.AttributesOf(setup));
    }
}
