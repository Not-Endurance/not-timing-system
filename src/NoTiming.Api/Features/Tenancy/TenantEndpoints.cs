using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Not.Identity;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Profile;
using NoTiming.Api.JsonApi;
using NTS.Domain.Access;
using NTS.Domain.Aggregates;
using NTS.Domain.Objects;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>
/// The account's place in the Tenants and the Tenants themselves (#643, ADR-0012). The account chooses the Tenant that
/// what it reads and writes belongs to from among the ones it is a member of. Any signed-in account reads a Tenant: its
/// name, whether it is operational and the rules of its Regional competitions. Root edits the rules, and what an
/// account may do in it is told by the same policy that refuses it. Nothing here gives a
/// role: roles are written by the Developer's command alone.
/// </summary>
internal static class TenantEndpoints
{
    const string ACCOUNTS = "accounts";
    const string TENANTS = "tenants";
    const string CAPABILITIES = "capabilities";
    const string SELECTED_TENANT_ID = "selectedTenantId";

    public static IEndpointRouteBuilder MapTenancy(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/me", SelectTenant);
        app.MapGet("/api/tenants/{id}", ReadTenant);
        app.MapPatch("/api/tenants/{id}", EditRules);
        app.MapGet("/api/tenants/{id}/capabilities", ReadCapabilities);
        return app;
    }

    // What an account does for itself is any signed-in account's.
    [Requires(Capability.SignedIn)]
    static async Task<IResult> SelectTenant(
        JsonApiBody<SelectionChange> body,
        AccessContext access,
        UserManager<NIdentityUser> users,
        ProfileStore store,
        TenantStore tenants,
        CancellationToken cancellationToken
    )
    {
        var user = access.User;
        if (body.Id != null && body.Id != user.Id.ToString())
        {
            return JsonApiResults.Error(
                StatusCodes.Status409Conflict,
                "account-id-mismatch",
                "The account is the caller's own: the id of the resource is not theirs."
            );
        }

        if (body.Attributes.Choice is { } choice)
        {
            var tenantId = choice.ValueKind == JsonValueKind.String ? choice.GetString() : null;
            if (tenantId != null && !await MayActIn(user, tenantId, tenants, cancellationToken))
            {
                return JsonApiResults.Error(
                    StatusCodes.Status422UnprocessableEntity,
                    "not-a-member",
                    "An account selects only a Tenant it is a member of."
                );
            }

            await store.SaveAsync(
                user.Id,
                new Dictionary<string, string?> { [AccountRoles.SELECTED_TENANT] = tenantId },
                cancellationToken
            );
        }

        var saved = (await users.FindByIdAsync(user.Id.ToString()))!;
        return MeResource.Of(saved, StatusCodes.Status200OK);
    }

    /// <summary>
    /// Whether the account may select the Tenant: it is a member of it, or it is the Developer and there is such a Tenant.
    /// </summary>
    static async Task<bool> MayActIn(
        NIdentityUser user,
        string tenantId,
        TenantStore tenants,
        CancellationToken cancellationToken
    )
    {
        return AccountRoles.MembershipsOf(user).Any(x => x.TenantId == tenantId)
            || (AccountRoles.IsDeveloper(user) && await tenants.FindAsync(tenantId, cancellationToken) is not null);
    }

    [Requires(Capability.SignedIn, PlaceIn.TenantRoute)]
    static IResult ReadTenant(AccessContext access)
    {
        return TenantResource(access.Tenant, access.IsOperational, StatusCodes.Status200OK);
    }

    [Requires(Capability.EditTenant, PlaceIn.TenantRoute)]
    static async Task<IResult> EditRules(
        JsonApiBody<TenantChange> body,
        AccessContext access,
        TenantStore tenants,
        TenancyLog log,
        CancellationToken cancellationToken
    )
    {
        var tenant = access.Tenant;
        var named = body.Attributes.Rules;
        var rules = named is null ? tenant.RegionalRules : TenantChange.Merge(tenant.RegionalRules, named.Value)!;
        if (await tenants.SetRulesAsync(tenant.Id, tenant.RegionalRules, rules, cancellationToken))
        {
            log.TenantRulesEdited(access.User.Id, tenant.Id, rules);
        }

        return TenantResource(tenant.WithRules(rules), access.IsOperational, StatusCodes.Status200OK);
    }

    [Requires(Capability.SignedIn, PlaceIn.TenantRoute)]
    static async Task<IResult> ReadCapabilities(
        AccessContext access,
        StandingReader standings,
        CancellationToken cancellationToken
    )
    {
        var id = access.Tenant.Id;
        var standing = await standings.AtTenantAsync(access.User, id, cancellationToken);
        var state = PlaceState.OfTenant(access.IsOperational);
        return JsonApiResults.Resource(
            StatusCodes.Status200OK,
            CAPABILITIES,
            id,
            new
            {
                level = AccessLevelText.Of(standing.Level),
                canCreateEvents = AccessPolicy.Decide(Capability.CreateEvent, standing, state).IsAllowed,
                canEditTenant = AccessPolicy.Decide(Capability.EditTenant, standing, state).IsAllowed,
                canEditRegistry = AccessPolicy.Decide(Capability.EditRegistry, standing, state).IsAllowed,
            }
        );
    }

    static IResult TenantResource(Tenant tenant, bool isOperational, int status)
    {
        return JsonApiResults.Resource(
            status,
            TENANTS,
            tenant.Id,
            new
            {
                name = tenant.Name,
                kind = tenant.Kind,
                isOperational,
                regionalRules = new
                {
                    onlyAverageLoopSpeed = tenant.RegionalRules.OnlyAverageLoopSpeed,
                    rankerCode = tenant.RegionalRules.RankerCode,
                },
            }
        );
    }
}
