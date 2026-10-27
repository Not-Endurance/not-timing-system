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
        app.MapGet("/api/configure-events", ListAsync);
        app.MapGet("/api/configure-events/{id}", ReadAsync);
        app.MapPatch("/api/configure-events/{id}", ChangeAsync);
        app.MapDelete("/api/configure-events/{id}", RemoveAsync);
        return app;
    }

    static ReferenceMembers<ConfigureEventModel> Members => SetupDocument.Members;

    [Requires(Capability.SignedIn)]
    static async Task<IResult> ListAsync(
        HttpContext context,
        AccessContext access,
        TenantCollections tenants,
        EventGrantStore grants
    )
    {
        var user = access.User;
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
        var rows = await tenants
            .Of<ConfigureEventModel>(TenantOwned.CONFIGURE_EVENTS, tenantId)
            .ReadAsync(
                running == null ? null : x => running.Contains(x.Id),
                query.Options,
                query.Skip,
                query.Take,
                context.RequestAborted
            );
        return JsonApiResults.Page(
            CONFIGURE_EVENTS,
            rows.Take(query.Size).Select(x => (x.Id.ToString(), (object)Members.AttributesOf(x))),
            context.Request,
            query.Size,
            query.Number,
            rows.Count > query.Size
        );
    }

    [Requires(Capability.ReadConfiguration, PlaceIn.EventRoute)]
    static async Task<IResult> ReadAsync(
        AccessContext access,
        TenantCollections tenants,
        CancellationToken cancellationToken
    )
    {
        var facts = access.Event;
        var setup = await Open(tenants, facts).FindAsync(facts.Id, cancellationToken);
        return setup is null ? JsonApiResults.NotFound() : Resource(setup, facts, StatusCodes.Status200OK);
    }

    [Requires(Capability.EditSetup, PlaceIn.EventRoute)]
    static async Task<IResult> ChangeAsync(
        JsonApiBody<SetupChange> body,
        AccessContext access,
        TenantCollections tenants,
        CancellationToken cancellationToken
    )
    {
        var facts = access.Event;
        var collection = Open(tenants, facts);
        var setup = await collection.FindAsync(facts.Id, cancellationToken);
        if (setup is null)
        {
            return JsonApiResults.NotFound();
        }

        var failed = await ReferenceRows.ChangeAsync<ConfigureEventModel, ConfigureEvent>(
            Members,
            collection,
            setup,
            body.Attributes.Members ?? [],
            cancellationToken
        );
        return failed ?? Resource(setup, facts, StatusCodes.Status200OK);
    }

    [Requires(Capability.DeleteEvent, PlaceIn.EventRoute)]
    static async Task<IResult> RemoveAsync(
        AccessContext access,
        TenantCollections tenants,
        EventGrantStore grants,
        BrandingStore branding,
        TenancyLog log,
        CancellationToken cancellationToken
    )
    {
        var facts = access.Event;
        if (!await Open(tenants, facts).DeleteAsync(facts.Id, cancellationToken))
        {
            return JsonApiResults.NotFound();
        }

        // What was given on the Event goes with it, so that no grant is left waiting for an Event that is not there.
        await grants.RemoveAllOfAsync(facts.Record, cancellationToken);
        await branding.RemoveOfEventAsync(facts.TenantId, facts.Id, cancellationToken); // and the logos it put in place of the Tenant's
        log.EventDeleted(access.User.Id, facts.Id, facts.TenantId);
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
