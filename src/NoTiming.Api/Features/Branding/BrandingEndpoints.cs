using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Not.Identity;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Events;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.Branding;

/// <summary>
/// The logos that go at the head of what an Event prints (#646, ADR-0012): the Tenant's own, which a Tenant Root edits, and the
/// ones an Event puts in place of them, which its Main Operator edits while it has not ended. They are the <c>brandings</c>
/// resource of a Tenant and of an Event, a document of what it has for the left and for the right of the header, each as an
/// image sent as base64 (JSON:API has no body but a document), of at most 512 KiB and of a kind a browser prints. What a document
/// of an Event is printed with is the Event's logo for a position, and the Tenant's where it has none. A PATCH names the positions
/// it changes: a logo sets it, null clears it, and what it does not name stays.
/// </summary>
internal static class BrandingEndpoints
{
    const string BRANDINGS = "brandings";
    const string LEFT = "left";
    const string RIGHT = "right";

    public static IEndpointRouteBuilder MapBranding(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/tenants/{id}/branding", ReadOfTenant).RequireAuthorization();
        app.MapPatch("/api/tenants/{id}/branding", EditOfTenant).RequireAuthorization();
        app.MapGet("/api/events/{id}/branding", ReadOfEvent).RequireAuthorization();
        app.MapPatch("/api/events/{id}/branding", EditOfEvent).RequireAuthorization();
        return app;
    }

    static async Task<IResult> ReadOfTenant(
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        TenantStore tenants,
        BrandingStore branding
    )
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        if (await tenants.FindAsync(id, context.RequestAborted) is null)
        {
            return JsonApiResults.NotFound();
        }

        var verdict = await DecideForTenant(user, id, tenants, context.RequestAborted);
        return verdict.IsAllowed
            ? await TenantResource(id, branding, context.RequestAborted)
            : AccessResults.Refused(verdict);
    }

    static async Task<IResult> EditOfTenant(
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        TenantStore tenants,
        BrandingStore branding,
        TenancyLog log
    )
    {
        var read = await JsonApiRequests.ReadAsync<MemberAttributes>(context.Request, BRANDINGS);
        if (read.Error != null)
        {
            return read.Error;
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        if (await tenants.FindAsync(id, context.RequestAborted) is null)
        {
            return JsonApiResults.NotFound();
        }

        if (read.Id != null && read.Id != id)
        {
            return JsonApiResults.IdMismatch();
        }

        var verdict = await DecideForTenant(user, id, tenants, context.RequestAborted);
        if (!verdict.IsAllowed)
        {
            return AccessResults.Refused(verdict);
        }

        var (changes, refusal) = Changes(read.Attributes!.Members ?? []);
        if (refusal != null)
        {
            return refusal;
        }

        await branding.SetAsync(id, BrandingStore.KeyOfTenant(id), changes, context.RequestAborted);
        log.TenantBrandingEdited(user.Id, id, [.. changes.Keys.Select(x => x.ToString())]);
        return await TenantResource(id, branding, context.RequestAborted);
    }

    static async Task<IResult> ReadOfEvent(
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        EventStore events,
        BrandingStore branding
    )
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        var facts = Guid.TryParse(id, out var eventId) ? await events.FindAsync(eventId, context.RequestAborted) : null;
        return facts is null ? JsonApiResults.NotFound() : await EventResource(facts, branding, context.RequestAborted);
    }

    static async Task<IResult> EditOfEvent(
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        EventStore events,
        BrandingStore branding,
        TenancyLog log
    )
    {
        var read = await JsonApiRequests.ReadAsync<MemberAttributes>(context.Request, BRANDINGS);
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

        if (read.Id != null && read.Id != id)
        {
            return JsonApiResults.IdMismatch();
        }

        var verdict = AccessPolicy.Decide(
            Capability.EditEventBranding,
            AccountRoles.CallerOf(user),
            facts.ScopeFor(CallerGrants.None)
        );
        if (!verdict.IsAllowed)
        {
            return AccessResults.Refused(verdict);
        }

        var (changes, refusal) = Changes(read.Attributes!.Members ?? []);
        if (refusal != null)
        {
            return refusal;
        }

        await branding.SetAsync(facts.TenantId, BrandingStore.KeyOfEvent(eventId), changes, context.RequestAborted);
        log.EventBrandingEdited(user.Id, eventId, facts.TenantId, [.. changes.Keys.Select(x => x.ToString())]);
        return await EventResource(facts, branding, context.RequestAborted);
    }

    static async Task<Verdict> DecideForTenant(
        NIdentityUser user,
        string tenantId,
        TenantStore tenants,
        CancellationToken cancellationToken
    )
    {
        return AccessPolicy.Decide(
            Capability.EditTenantBranding,
            AccountRoles.CallerOf(user),
            AccessScope.ForTenant(tenantId, await tenants.IsOperationalAsync(tenantId, cancellationToken))
        );
    }

    /// <summary>The positions a document names, each with the logo it is given or none to clear it; the answer that refuses the document when anything in it is not valid.</summary>
    static (Dictionary<LogoPosition, Logo?> Changes, IResult? Refusal) Changes(
        IReadOnlyDictionary<string, JsonElement> members
    )
    {
        var changes = new Dictionary<LogoPosition, Logo?>();
        foreach (var (name, value) in members)
        {
            var position = PositionOf(name);
            if (position is null)
            {
                return (
                    changes,
                    JsonApiResults.Error(
                        StatusCodes.Status400BadRequest,
                        "unsupported-attribute",
                        "Only the left and the right logo can be changed.",
                        $"{LEFT}, {RIGHT}"
                    )
                );
            }

            if (changes.ContainsKey(position.Value))
            {
                return (
                    changes,
                    JsonApiResults.Error(
                        StatusCodes.Status400BadRequest,
                        "malformed-request",
                        "A logo is named once.",
                        name
                    )
                );
            }

            if (value.ValueKind == JsonValueKind.Null)
            {
                changes[position.Value] = null;
                continue;
            }

            var logo = Logo.Read(value);
            if (logo.Logo is null)
            {
                return (
                    changes,
                    JsonApiResults.Error(logo.Status, logo.Code!, "The logo cannot be kept.", logo.Detail)
                );
            }

            changes[position.Value] = logo.Logo;
        }

        return (changes, null);
    }

    static LogoPosition? PositionOf(string name)
    {
        if (string.Equals(name, LEFT, StringComparison.OrdinalIgnoreCase))
        {
            return LogoPosition.Left;
        }

        return string.Equals(name, RIGHT, StringComparison.OrdinalIgnoreCase) ? LogoPosition.Right : null;
    }

    static async Task<IResult> TenantResource(
        string tenantId,
        BrandingStore branding,
        CancellationToken cancellationToken
    )
    {
        var logos = await branding.ReadAsync(tenantId, BrandingStore.KeyOfTenant(tenantId), cancellationToken);
        return JsonApiResults.Resource(
            StatusCodes.Status200OK,
            BRANDINGS,
            tenantId,
            new { left = Shown(logos, LogoPosition.Left, null), right = Shown(logos, LogoPosition.Right, null) }
        );
    }

    /// <summary>What an Event is printed with: its own logo for a position, and the Tenant's where it has none.</summary>
    static async Task<IResult> EventResource(
        EventFacts facts,
        BrandingStore branding,
        CancellationToken cancellationToken
    )
    {
        var own = await branding.ReadAsync(facts.TenantId, BrandingStore.KeyOfEvent(facts.Id), cancellationToken);
        var tenants = await branding.ReadAsync(
            facts.TenantId,
            BrandingStore.KeyOfTenant(facts.TenantId),
            cancellationToken
        );
        return JsonApiResults.Resource(
            StatusCodes.Status200OK,
            BRANDINGS,
            facts.Id.ToString(),
            new
            {
                left = Effective(own, tenants, LogoPosition.Left),
                right = Effective(own, tenants, LogoPosition.Right),
            }
        );
    }

    static object? Effective(
        IReadOnlyDictionary<LogoPosition, Logo> own,
        IReadOnlyDictionary<LogoPosition, Logo> tenants,
        LogoPosition position
    )
    {
        return own.ContainsKey(position) ? Shown(own, position, "event") : Shown(tenants, position, "tenant");
    }

    static object? Shown(IReadOnlyDictionary<LogoPosition, Logo> logos, LogoPosition position, string? source)
    {
        return logos.TryGetValue(position, out var logo)
            ? new
            {
                contentType = logo.ContentType,
                content = Convert.ToBase64String(logo.Content),
                size = logo.Content.Length,
                source,
            }
            : null;
    }
}
