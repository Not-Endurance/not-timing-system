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
/// name, whether it is operational and the rules of its Regional competitions. A Tenant Root of it, or the Developer,
/// edits the rules, and what an account may do in it is told by the same policy that refuses it. Nothing here gives a
/// role: roles are written by the Developer's command alone.
/// </summary>
internal static class TenantEndpoints
{
    const string ACCOUNTS = "accounts";
    const string TENANTS = "tenants";
    const string CAPABILITIES = "capabilities";
    const string SELECTED_TENANT_ID = "selectedTenantId";
    const string REGIONAL_RULES = "regionalRules";

    public static IEndpointRouteBuilder MapTenancy(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/me", SelectTenant).RequireAuthorization();
        app.MapGet("/api/tenants/{id}", ReadTenant).RequireAuthorization();
        app.MapPatch("/api/tenants/{id}", EditRules).RequireAuthorization();
        app.MapGet("/api/tenants/{id}/capabilities", ReadCapabilities).RequireAuthorization();
        return app;
    }

    static async Task<IResult> SelectTenant(
        HttpContext context,
        UserManager<NIdentityUser> users,
        ProfileStore store,
        TenantStore tenants
    )
    {
        var read = await JsonApiRequests.ReadAsync<MemberAttributes>(context.Request, ACCOUNTS);
        if (read.Error != null)
        {
            return read.Error;
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        if (read.Id != null && read.Id != user.Id.ToString())
        {
            return JsonApiResults.Error(
                StatusCodes.Status409Conflict,
                "account-id-mismatch",
                "The account is the caller's own: the id of the resource is not theirs."
            );
        }

        var members = read.Attributes!.Members ?? [];
        if (members.Keys.Any(x => !string.Equals(x, SELECTED_TENANT_ID, StringComparison.OrdinalIgnoreCase)))
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "unsupported-attribute",
                "Only the selected Tenant of an account can be changed.",
                SELECTED_TENANT_ID
            );
        }

        var named = members.FirstOrDefault(x =>
            string.Equals(x.Key, SELECTED_TENANT_ID, StringComparison.OrdinalIgnoreCase)
        );
        if (named.Key != null)
        {
            var choice = named.Value;
            if (choice.ValueKind is not (JsonValueKind.Null or JsonValueKind.String))
            {
                return JsonApiResults.Error(
                    StatusCodes.Status400BadRequest,
                    "malformed-request",
                    "The selected Tenant is the id of a Tenant, or null to go back to the home Tenant."
                );
            }

            var tenantId = choice.ValueKind == JsonValueKind.String ? choice.GetString() : null;
            if (tenantId != null && !await MayActIn(user, tenantId, tenants, context.RequestAborted))
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
                context.RequestAborted
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

    static async Task<IResult> ReadTenant(string id, HttpContext context, TenantStore tenants)
    {
        var tenant = await tenants.FindAsync(id, context.RequestAborted);
        return tenant is null
            ? JsonApiResults.NotFound()
            : TenantResource(
                tenant,
                await tenants.IsOperationalAsync(id, context.RequestAborted),
                StatusCodes.Status200OK
            );
    }

    static async Task<IResult> EditRules(
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        TenantStore tenants,
        TenancyLog log
    )
    {
        var read = await JsonApiRequests.ReadAsync<MemberAttributes>(context.Request, TENANTS);
        if (read.Error != null)
        {
            return read.Error;
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        var tenant = await tenants.FindAsync(id, context.RequestAborted);
        if (tenant is null)
        {
            return JsonApiResults.NotFound();
        }

        if (read.Id != null && read.Id != id)
        {
            return JsonApiResults.Error(
                StatusCodes.Status409Conflict,
                "tenant-id-mismatch",
                "The id of the resource is not the one of the route."
            );
        }

        var operational = await tenants.IsOperationalAsync(id, context.RequestAborted);
        var verdict = AccessPolicy.Decide(
            Capability.EditTenantRules,
            AccountRoles.CallerOf(user),
            AccessScope.ForTenant(id, operational)
        );
        if (!verdict.IsAllowed)
        {
            return AccessResults.Refused(verdict);
        }

        var members = read.Attributes!.Members ?? [];
        if (members.Keys.Any(x => !string.Equals(x, REGIONAL_RULES, StringComparison.OrdinalIgnoreCase)))
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "unsupported-attribute",
                "Only the rules of the Regional competitions of a Tenant can be changed.",
                REGIONAL_RULES
            );
        }

        var named = members.FirstOrDefault(x =>
            string.Equals(x.Key, REGIONAL_RULES, StringComparison.OrdinalIgnoreCase)
        );
        var rules = named.Key == null ? tenant.RegionalRules : Merge(tenant.RegionalRules, named.Value);
        if (rules is null)
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "invalid-rules",
                "The rules are onlyAverageLoopSpeed (true or false) and rankerCode (a short text, or null for none)."
            );
        }

        if (await tenants.SetRulesAsync(id, tenant.RegionalRules, rules, context.RequestAborted))
        {
            log.TenantRulesEdited(user.Id, id, rules);
        }

        return TenantResource(tenant.WithRules(rules), operational, StatusCodes.Status200OK);
    }

    static async Task<IResult> ReadCapabilities(
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        TenantStore tenants,
        CallerReader callers
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

        var caller = await callers.ReadAsync(user, context.RequestAborted);
        var scope = AccessScope.ForTenant(id, await tenants.IsOperationalAsync(id, context.RequestAborted));
        return JsonApiResults.Resource(
            StatusCodes.Status200OK,
            CAPABILITIES,
            id,
            new
            {
                canCreateEvents = AccessPolicy.Decide(Capability.CreateEvent, caller, scope).IsAllowed,
                canEditRules = AccessPolicy.Decide(Capability.EditTenantRules, caller, scope).IsAllowed,
                canEditBranding = AccessPolicy.Decide(Capability.EditTenantBranding, caller, scope).IsAllowed,
                canEditRegistry = AccessPolicy.Decide(Capability.EditRegistry, caller, scope).IsAllowed,
                canSearchAccounts = AccessPolicy.Decide(Capability.SearchAccounts, caller, scope).IsAllowed,
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

    /// <summary>
    /// The rules once the members of the document are applied: a member that is not named stays as it is, and a ranker
    /// that is null is cleared. Null when anything is not valid, and then nothing is saved.
    /// </summary>
    static RegionalRules? Merge(RegionalRules current, JsonElement document)
    {
        if (document.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var onlyAverageLoopSpeed = current.OnlyAverageLoopSpeed;
        var rankerCode = current.RankerCode;
        foreach (var member in document.EnumerateObject())
        {
            if (string.Equals(member.Name, "onlyAverageLoopSpeed", StringComparison.OrdinalIgnoreCase))
            {
                if (member.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    return null;
                }

                onlyAverageLoopSpeed = member.Value.GetBoolean();
            }
            else if (string.Equals(member.Name, "rankerCode", StringComparison.OrdinalIgnoreCase))
            {
                if (member.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.String))
                {
                    return null;
                }

                rankerCode = member.Value.ValueKind == JsonValueKind.Null ? null : member.Value.GetString();
            }
            else
            {
                return null;
            }
        }

        try
        {
            return new RegionalRules(onlyAverageLoopSpeed, rankerCode);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}

/// <summary>
/// The members of a document that a PATCH names, as they were sent: "not named" and "null" mean different things, the
/// first leaves a member alone and the second clears it.
/// </summary>
internal sealed class MemberAttributes
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Members { get; set; }
}
