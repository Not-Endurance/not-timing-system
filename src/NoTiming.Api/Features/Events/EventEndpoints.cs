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
    const int MAX_NAME_LENGTH = 100;
    const int MAX_FEI_SHOW_ID_LENGTH = 20;

    public static IEndpointRouteBuilder MapEvents(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/configure-events", Create).RequireAuthorization();
        app.MapGet("/api/events/{id}/capabilities", ReadCapabilities).RequireAuthorization();
        return app;
    }

    static async Task<IResult> Create(
        HttpContext context,
        UserManager<NIdentityUser> users,
        StandingReader standing,
        TenantStore tenants,
        TenantCollections collections,
        EventStore events,
        TenancyLog log
    )
    {
        var read = await JsonApiRequests.ReadAsync<NewEventAttributes>(context.Request, CONFIGURE_EVENTS);
        if (read.Error != null)
        {
            return read.Error;
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        // What the account does in is its current Tenant: nothing that is sent names another.
        var tenantId = AccountRoles.CurrentTenantOf(user);
        if (tenantId == null || await tenants.FindAsync(tenantId, context.RequestAborted) is null)
        {
            return JsonApiResults.Error(
                StatusCodes.Status409Conflict,
                "no-current-tenant",
                "Choose a country on your profile before you make an Event: an Event belongs to the Tenant of one."
            );
        }

        var verdict = await standing.DecideAtTenantAsync(
            Capability.CreateEvent,
            user,
            tenantId,
            await tenants.IsOperationalAsync(tenantId, context.RequestAborted),
            context.RequestAborted
        );
        if (!verdict.IsAllowed)
        {
            return AccessResults.Refused(verdict);
        }

        var attributes = read.Attributes!;
        if (attributes.Others is { Count: > 0 })
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "unsupported-attribute",
                "An Event is made with a name, a location and optionally an FEI show ID, and with nothing else.",
                string.Join(", ", attributes.Others.Keys)
            );
        }

        var name = attributes.Name?.Trim();
        if (string.IsNullOrEmpty(name) || !OneLineText.IsValid(name, MAX_NAME_LENGTH))
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "invalid-name",
                "An Event needs a name of one line of at most 100 characters."
            );
        }

        var location = attributes.Location?.Trim();
        if (string.IsNullOrEmpty(location) || !OneLineText.IsValid(location, MAX_NAME_LENGTH))
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "invalid-location",
                "An Event needs a location of one line of at most 100 characters."
            );
        }

        var feiShowId = string.IsNullOrWhiteSpace(attributes.FeiShowId) ? null : attributes.FeiShowId.Trim();
        if (feiShowId != null && !OneLineText.IsValid(feiShowId, MAX_FEI_SHOW_ID_LENGTH))
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "invalid-fei-show-id",
                "An FEI show ID is at most 20 characters of one line."
            );
        }

        Guid? requested = null;
        if (read.Id != null)
        {
            if (!Guid.TryParse(read.Id, out var named))
            {
                return JsonApiResults.InvalidId();
            }

            requested = named;
        }

        try
        {
            var (id, created) = await events.CreateAsync(
                tenantId,
                requested,
                name,
                location,
                feiShowId,
                context.RequestAborted
            );
            if (!created)
            {
                // The same id again is the Event that was made. An Event of another Tenant with it is not told about.
                var existing = await collections
                    .Of<ConfigureEventModel>(TenantOwned.CONFIGURE_EVENTS, tenantId)
                    .FindAsync(id, context.RequestAborted);
                return existing is null
                    ? JsonApiResults.IdTaken()
                    : JsonApiResults.Resource(
                        StatusCodes.Status200OK,
                        CONFIGURE_EVENTS,
                        id.ToString(),
                        SetupEndpoints.Members.AttributesOf(existing)
                    );
            }

            log.EventCreated(user.Id, id, tenantId);
            return JsonApiResults.Resource(
                StatusCodes.Status201Created,
                CONFIGURE_EVENTS,
                id.ToString(),
                new
                {
                    name,
                    location,
                    feiShowId,
                    tenantId,
                },
                $"/api/{CONFIGURE_EVENTS}/{id}"
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

    static async Task<IResult> ReadCapabilities(
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        EventStore events,
        StandingReader standings,
        TenantStore tenants
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

        var standing = await standings.AtEventAsync(user, facts, context.RequestAborted);
        var inTheTenant = await standings.AtTenantAsync(user, facts.TenantId, context.RequestAborted);
        var tenant = PlaceState.OfTenant(await tenants.IsOperationalAsync(facts.TenantId, context.RequestAborted));
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

/// <summary>What a new Event is made of. A member that is not one of these is kept, to be refused.</summary>
internal sealed class NewEventAttributes
{
    public string? Name { get; set; }
    public string? Location { get; set; }
    public string? FeiShowId { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Others { get; set; }
}
