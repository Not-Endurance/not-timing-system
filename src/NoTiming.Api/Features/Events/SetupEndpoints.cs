using Microsoft.AspNetCore.Identity;
using Not.Identity;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Branding;
using NoTiming.Api.Features.Profile;
using NoTiming.Api.Features.Reference;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Access;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Api.Features.Events;

/// <summary>
/// The Setup of an Event, the <c>configure-events</c> resource (#603, ADR-0012): what an Event is configured with until it
/// starts. Root makes the Event, in the Tenant it is acting in, with a name and a location and nothing else: it cannot be
/// given a Tenant by what is sent, and the rest of its Setup is changed here (#643). This reads, changes and deletes it. It is
/// read by a Console Operator of the Event and by Root; changed by them until the Event starts, and then by nobody, with 409
/// and the stage the Event is at, because from the start the Console works on the copies the Event made of it; deleted, with
/// the grants of the Event, by Root before it starts. The Tenant of the Event is shown and cannot be written. The members and
/// the filter, the sort and the pages of a list are those of every family of reference data (<see cref="ReferenceEndpoints"/>).
/// </summary>
internal static class SetupEndpoints
{
    const string CONFIGURE_EVENTS = "configure-events";
    const int MAX_NAME_LENGTH = 100;
    const int MAX_FEI_SHOW_ID_LENGTH = 20;

    public static IEndpointRouteBuilder MapSetups(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/configure-events", PostConfigureEvents);
        app.MapGet("/api/configure-events", GetConfigureEvents);
        app.MapGet("/api/configure-events/{id}", GetConfigureEventsById);
        app.MapPatch("/api/configure-events/{id}", PatchConfigureEventsById);
        app.MapDelete("/api/configure-events/{id}", DeleteConfigureEventsById);
        return app;
    }

    static ReferenceMembers<ConfigureEventModel> Members => SetupDocument.Members;

    // The Event is made in the current Tenant of the account, with the Setup it is sent with: nothing that is sent names another.
    [Requires(Capability.CreateEvent, PlaceIn.CurrentTenant)]
    static async Task<IResult> PostConfigureEvents(
        JsonApiBody<NewMembers> body,
        AccessContext access,
        TenantCollections collections,
        EventStore events,
        TenancyLog log,
        CancellationToken cancellationToken
    )
    {
        var tenantId = access.Tenant.Id;
        var id = body.Id is null ? Guid.NewGuid() : Guid.Parse(body.Id);
        var setup = new ConfigureEventModel
        {
            Competitions = [],
            Officials = [],
            Loops = [],
            Combinations = [],
        };
        if (Members.Apply(setup, body.Attributes.Members ?? [], []) is { } malformed)
        {
            return malformed;
        }

        if (Unacceptable(setup) is { } unacceptable)
        {
            return unacceptable;
        }

        ReferenceRows.SetId(setup, id);
        var setups = collections.Of<ConfigureEventModel>(TenantOwned.CONFIGURE_EVENTS, tenantId);
        try
        {
            setup.Country ??= await events.CountryOfAsync(tenantId, cancellationToken);
            if (ReferenceRows.Invalid<ConfigureEventModel, ConfigureEvent>(setup) is { } invalid)
            {
                return invalid;
            }

            if (await setups.InsertAsync(setup, cancellationToken))
            {
                log.EventCreated(access.User.Id, id, tenantId);
                return JsonApiResults.Resource(
                    StatusCodes.Status201Created,
                    CONFIGURE_EVENTS,
                    id.ToString(),
                    Members.AttributesOf(setup),
                    $"/api/{CONFIGURE_EVENTS}/{id}"
                );
            }

            // The same id again is the Setup that was made. An Event of another Tenant with it is not told about.
            var existing = await setups.FindAsync(id, cancellationToken);
            return existing is null
                ? JsonApiResults.IdTaken()
                : JsonApiResults.Resource(
                    StatusCodes.Status200OK,
                    CONFIGURE_EVENTS,
                    id.ToString(),
                    Members.AttributesOf(existing)
                );
        }
        catch (TenantCountryMissingException)
        {
            return EventsFailures.TenantCountryMissing.AsResult();
        }
    }

    /// <summary>
    /// What an Event needs whatever else it is made with: a name and a location of one line each, and a FEI show ID that
    /// is no longer than one can be. What is accepted is put into the Setup trimmed.
    /// </summary>
    static IResult? Unacceptable(ConfigureEventModel setup)
    {
        var name = setup.Name?.Trim();
        if (string.IsNullOrEmpty(name) || !OneLineText.IsValid(name, MAX_NAME_LENGTH))
        {
            return EventsFailures.InvalidName.AsResult();
        }

        var location = setup.Location?.Trim();
        if (string.IsNullOrEmpty(location) || !OneLineText.IsValid(location, MAX_NAME_LENGTH))
        {
            return EventsFailures.InvalidLocation.AsResult();
        }

        var feiShowId = string.IsNullOrWhiteSpace(setup.FeiShowId) ? null : setup.FeiShowId.Trim();
        if (feiShowId != null && !OneLineText.IsValid(feiShowId, MAX_FEI_SHOW_ID_LENGTH))
        {
            return EventsFailures.InvalidFeiShowId.AsResult();
        }

        setup.Name = name;
        setup.Location = location;
        setup.FeiShowId = feiShowId;
        return null;
    }

    [Requires(Capability.SignedIn)]
    static async Task<IResult> GetConfigureEvents(
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
    static async Task<IResult> GetConfigureEventsById(
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
    static async Task<IResult> PatchConfigureEventsById(
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
    static async Task<IResult> DeleteConfigureEventsById(
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
