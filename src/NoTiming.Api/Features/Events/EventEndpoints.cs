using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Not.Identity;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Profile;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.Events;

/// <summary>
/// Creating an Event, and who runs it (#643, ADR-0012). A Tenant Root makes an Event in the Tenant it is acting in and is
/// its Main Operator; it assigns another account while the Event has not started, and once the Event is Live only the
/// Main Operator hands it over. What a person may do about an Event is told by <c>GET /api/events/{id}/capabilities</c>
/// from the same policy that refuses it. The Event is made with a name and a location and nothing else: it cannot be given
/// a Tenant or a Main Operator by what is sent, and the rest of its Setup is the Setup's own routes.
/// </summary>
internal static class EventEndpoints
{
    const string CONFIGURE_EVENTS = "configure-events";
    const string MAIN_OPERATORS = "main-operators";
    const string CAPABILITIES = "capabilities";
    const int MAX_NAME_LENGTH = 100;
    const int MAX_FEI_SHOW_ID_LENGTH = 20;

    public static IEndpointRouteBuilder MapEvents(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/configure-events", Create).RequireAuthorization();
        app.MapPost("/api/events/{id}/actions/assign-main-operator", AssignMainOperator).RequireAuthorization();
        app.MapPost("/api/events/{id}/actions/hand-over", HandOver).RequireAuthorization();
        app.MapGet("/api/events/{id}/capabilities", ReadCapabilities).RequireAuthorization();
        return app;
    }

    static async Task<IResult> Create(
        HttpContext context,
        UserManager<NIdentityUser> users,
        TenantStore tenants,
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

        var verdict = AccessPolicy.Decide(
            Capability.CreateEvent,
            AccountRoles.CallerOf(user),
            AccessScope.ForTenant(tenantId, await tenants.IsOperationalAsync(tenantId, context.RequestAborted))
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

        try
        {
            var id = await events.CreateAsync(tenantId, user.Id, name, location, feiShowId, context.RequestAborted);
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
                    mainOperatorId = user.Id,
                }
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

    static Task<IResult> AssignMainOperator(
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        EventStore events,
        TenancyLog log
    )
    {
        return ChangeMainOperator(id, Capability.AssignMainOperator, context, users, events, log, assign: true);
    }

    static Task<IResult> HandOver(
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        EventStore events,
        TenancyLog log
    )
    {
        return ChangeMainOperator(id, Capability.HandOverMainOperator, context, users, events, log, assign: false);
    }

    static async Task<IResult> ChangeMainOperator(
        string id,
        Capability capability,
        HttpContext context,
        UserManager<NIdentityUser> users,
        EventStore events,
        TenancyLog log,
        bool assign
    )
    {
        var read = await JsonApiRequests.ReadAsync<MainOperatorAttributes>(context.Request, MAIN_OPERATORS);
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

        var verdict = AccessPolicy.Decide(capability, AccountRoles.CallerOf(user), facts.ScopeFor(CallerGrants.None));
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
                "The account that is to be the Main Operator is sent as accountId, and nothing else.",
                string.Join(", ", attributes.Others.Keys)
            );
        }

        if (attributes.AccountId is not { } accountId)
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "malformed-request",
                "Send the account that is to be the Main Operator as accountId."
            );
        }

        var account = await users.FindByIdAsync(accountId.ToString());
        if (account is null)
        {
            return JsonApiResults.Error(
                StatusCodes.Status422UnprocessableEntity,
                "account-not-found",
                "There is no such account."
            );
        }

        var changed = assign
            ? await events.AssignMainOperatorAsync(facts.Record, account.Id, context.RequestAborted)
            : await events.HandOverAsync(facts.Record, account.Id, context.RequestAborted);
        if (!changed)
        {
            return JsonApiResults.NotFound();
        }

        if (assign)
        {
            log.MainOperatorAssigned(user.Id, eventId, account.Id);
        }
        else
        {
            log.EventHandedOver(user.Id, eventId, account.Id);
        }

        return Results.NoContent();
    }

    static async Task<IResult> ReadCapabilities(
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        EventStore events,
        EventGrantStore grants,
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

        var caller = AccountRoles.CallerOf(user);
        var scope = facts.ScopeFor(await grants.GrantsOfAsync(facts.Record, user.Id, context.RequestAborted));
        var tenant = AccessScope.ForTenant(
            facts.TenantId,
            await tenants.IsOperationalAsync(facts.TenantId, context.RequestAborted)
        );
        return JsonApiResults.Resource(
            StatusCodes.Status200OK,
            CAPABILITIES,
            facts.Id.ToString(),
            new
            {
                stage = facts.Stage.ToString().ToLowerInvariant(),
                isMainOperator = AccessPolicy.IsMainOperator(caller, scope),
                canSnapshot = AccessPolicy.Decide(Capability.SendSnapshot, caller, scope).IsAllowed,
                canHandOver = AccessPolicy.Decide(Capability.HandOverMainOperator, caller, scope).IsAllowed,
                canAssignMainOperator = AccessPolicy.Decide(Capability.AssignMainOperator, caller, scope).IsAllowed,
                canCreateEvents = AccessPolicy.Decide(Capability.CreateEvent, caller, tenant).IsAllowed,
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

/// <summary>The account that is to be the Main Operator. A member that is not it is kept, to be refused.</summary>
internal sealed class MainOperatorAttributes
{
    public Guid? AccountId { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Others { get; set; }
}
