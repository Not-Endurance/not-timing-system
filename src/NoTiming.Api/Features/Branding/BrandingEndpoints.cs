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
/// The logos that go at the head of what an Event prints (#646, ADR-0012): the Tenant's own, which Root edits, and the
/// ones an Event puts in place of them, which Root and the Console Operators of the Event edit while it has not ended. They are the <c>brandings</c>
/// resource of a Tenant and of an Event, a document of what it has for the left and for the right of the header, each as an
/// image sent as base64 (JSON:API has no body but a document), of at most 512 KiB and of a kind a browser prints. What a document
/// of an Event is printed with is the Event's logo for a position, and the Tenant's where it has none. A PATCH names the positions
/// it changes: a logo sets it, null clears it, and what it does not name stays.
/// </summary>
internal static class BrandingEndpoints
{
    const string BRANDINGS = "brandings";

    public static IEndpointRouteBuilder MapBranding(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/tenants/{id}/branding", GetTenantsByIdBranding);
        app.MapPatch("/api/tenants/{id}/branding", PatchTenantsByIdBranding);
        app.MapGet("/api/events/{id}/branding", GetEventsByIdBranding);
        app.MapPatch("/api/events/{id}/branding", PatchEventsByIdBranding);
        return app;
    }

    // The logos of a Tenant are Root's to see and to edit.
    [Requires(Capability.EditTenant, PlaceIn.TenantRoute)]
    static async Task<IResult> GetTenantsByIdBranding(
        AccessContext access,
        BrandingStore branding,
        CancellationToken cancellationToken
    )
    {
        return await TenantResource(access.Tenant.Id, branding, cancellationToken);
    }

    [Requires(Capability.EditTenant, PlaceIn.TenantRoute)]
    static async Task<IResult> PatchTenantsByIdBranding(
        JsonApiBody<BrandingChange> body,
        AccessContext access,
        BrandingStore branding,
        TenancyLog log,
        CancellationToken cancellationToken
    )
    {
        var id = access.Tenant.Id;
        var changes = body.Attributes.Logos;
        await branding.SetAsync(id, BrandingStore.KeyOfTenant(id), changes, cancellationToken);
        log.TenantBrandingEdited(access.User.Id, id, [.. changes.Keys.Select(x => x.ToString())]);
        return await TenantResource(id, branding, cancellationToken);
    }

    // What an Event is printed with is read by every signed-in account.
    [Requires(Capability.SignedIn, PlaceIn.EventRoute)]
    static async Task<IResult> GetEventsByIdBranding(
        AccessContext access,
        BrandingStore branding,
        CancellationToken cancellationToken
    )
    {
        return await EventResource(access.Event, branding, cancellationToken);
    }

    [Requires(Capability.ConfigureEvent, PlaceIn.EventRoute)]
    static async Task<IResult> PatchEventsByIdBranding(
        JsonApiBody<BrandingChange> body,
        AccessContext access,
        BrandingStore branding,
        TenancyLog log,
        CancellationToken cancellationToken
    )
    {
        var facts = access.Event;
        var changes = body.Attributes.Logos;
        await branding.SetAsync(facts.TenantId, BrandingStore.KeyOfEvent(facts.Id), changes, cancellationToken);
        log.EventBrandingEdited(access.User.Id, facts.Id, facts.TenantId, [.. changes.Keys.Select(x => x.ToString())]);
        return await EventResource(facts, branding, cancellationToken);
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
