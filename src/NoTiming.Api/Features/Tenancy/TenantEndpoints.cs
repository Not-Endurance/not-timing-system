using NoTiming.Api.Features.Access;
using NoTiming.Api.JsonApi;
using NTS.Domain.Access;
using NTS.Domain.Aggregates;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>
/// The Tenants themselves (#643, ADR-0012). Any signed-in account reads a Tenant: its name, whether it is operational and the
/// rules of its Regional competitions. Root edits the rules. Nothing here gives a role: roles are written by the Developer's
/// command alone.
/// </summary>
internal static class TenantEndpoints
{
    const string TENANTS = "tenants";
    const string CAPABILITIES = "capabilities";

    public static IEndpointRouteBuilder MapTenants(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/tenants/{id}", GetTenantsById);
        app.MapPatch("/api/tenants/{id}", PatchTenantsById);
        app.MapGet("/api/tenants/{id}/capabilities", GetTenantsByIdCapabilities);
        return app;
    }

    [Requires(Capability.SignedIn, PlaceIn.TenantRoute)]
    static IResult GetTenantsById(AccessContext access)
    {
        return TenantResource(access.Tenant, access.IsOperational, StatusCodes.Status200OK);
    }

    [Requires(Capability.EditTenant, PlaceIn.TenantRoute)]
    static async Task<IResult> PatchTenantsById(
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
    static async Task<IResult> GetTenantsByIdCapabilities(
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
