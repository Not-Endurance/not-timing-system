using Microsoft.AspNetCore.Identity;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Access;
using NTS.Domain.Setup.Aggregates;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Profile;
using NoTiming.Api.Features.Reference;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using Not.Identity;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace NoTiming.Api.Features.Events;

/// <summary>
/// Creating an Event, and what a person may do about one (#643, ADR-0012). Root makes an Event in the Tenant it is acting
/// in, with its Setup in one request, as a change of the Setup names it: the members it may name are the ones it is made
/// with. Console Operators run it, assigned after it (<c>/api/console-operators</c>). What a person may do about an Event is
/// told by <c>GET /api/events/{id}/capabilities</c> from the same policy that refuses it. The Event cannot be given a Tenant
/// by what is sent.
/// </summary>
internal static class EventEndpoints
{
    const string CONFIGURE_EVENTS = "configure-events";
    const string CAPABILITIES = "capabilities";
    const int MAX_NAME_LENGTH = 100;
    const int MAX_FEI_SHOW_ID_LENGTH = 20;

    public static IEndpointRouteBuilder MapEvents(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/configure-events", Create);
        app.MapGet("/api/events/{id}/capabilities", ReadCapabilities);
        return app;
    }

    // The Event is made in the current Tenant of the account, with the Setup it is sent with: nothing that is sent names another.
    [Requires(Capability.CreateEvent, PlaceIn.CurrentTenant)]
    static async Task<IResult> Create(
        JsonApiBody<NewMembers> body,
        AccessContext access,
        TenantCollections collections,
        EventStore events,
        TenancyLog log,
        CancellationToken cancellationToken
    )
    {
        var tenantId = access.Tenant.Id;
        var setup = new ConfigureEventModel
        {
            Competitions = [],
            Officials = [],
            Loops = [],
            Combinations = [],
        };
        if (SetupEndpoints.Members.Apply(setup, body.Attributes.Members ?? [], []) is { } malformed)
        {
            return malformed;
        }

        if (Unacceptable(setup) is { } unacceptable)
        {
            return unacceptable;
        }

        var id = body.Id is null ? Guid.NewGuid() : Guid.Parse(body.Id);
        setup.Id = id;
        var setups = collections.Of<ConfigureEventModel>(TenantOwned.CONFIGURE_EVENTS, tenantId);
        try
        {
            setup.Country ??= await events.CountryOfAsync(tenantId, cancellationToken);
            if (ReferenceEndpoints.Invalid<ConfigureEventModel, ConfigureEvent>(setup) is { } invalid)
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
                    SetupEndpoints.Members.AttributesOf(setup),
                    $"/api/{CONFIGURE_EVENTS}/{id}"
                );
            }

            // The same id again is the Event that was made. An Event of another Tenant with it is not told about.
            var existing = await setups.FindAsync(id, cancellationToken);
            return existing is null
                ? JsonApiResults.IdTaken()
                : JsonApiResults.Resource(
                    StatusCodes.Status200OK,
                    CONFIGURE_EVENTS,
                    id.ToString(),
                    SetupEndpoints.Members.AttributesOf(existing)
                );
        }
        catch (TenantCountryMissingException)
        {
            return JsonApiResults.Error(
                StatusCodes.Status409Conflict,
                "tenant-country-missing",
                "The country of the Tenant is not among the countries, so no Event can be made in it."
            );
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
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "invalid-name",
                "An Event needs a name of one line of at most 100 characters."
            );
        }

        var location = setup.Location?.Trim();
        if (string.IsNullOrEmpty(location) || !OneLineText.IsValid(location, MAX_NAME_LENGTH))
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "invalid-location",
                "An Event needs a location of one line of at most 100 characters."
            );
        }

        var feiShowId = string.IsNullOrWhiteSpace(setup.FeiShowId) ? null : setup.FeiShowId.Trim();
        if (feiShowId != null && !OneLineText.IsValid(feiShowId, MAX_FEI_SHOW_ID_LENGTH))
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "invalid-fei-show-id",
                "An FEI show ID is at most 20 characters of one line."
            );
        }

        setup.Name = name;
        setup.Location = location;
        setup.FeiShowId = feiShowId;
        return null;
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
