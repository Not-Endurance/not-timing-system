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
    [Requires(Capability.SignedIn, PlaceIn.Platform)]
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

/// <summary>
/// The members of a document that a PATCH names, as they were sent: "not named" and "null" mean different things, the
/// first leaves a member alone and the second clears it.
/// </summary>
internal class MemberAttributes : IResourceAttributes
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Members { get; set; }

    /// <summary>The members are checked against the row they are applied to, which only the handler has.</summary>
    public virtual JsonApiFailure? Invalid()
    {
        return null;
    }
}

/// <summary>
/// The change of the rules of a Tenant: the members of the document are the rules and nothing else. A member that is not
/// named stays as it is, and a ranker that is null is cleared. Its id, when the document gives one, is the one of the route.
/// </summary>
internal sealed class TenantChange : MemberAttributes, IResourceAttributes
{
    const string REGIONAL_RULES = "regionalRules";

    /// <summary>
    /// The rules once the members of the document are applied to the current ones. Null when anything is not valid, and then
    /// nothing is saved.
    /// </summary>
    public static RegionalRules? Merge(RegionalRules current, JsonElement document)
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

    public static string? ResourceType => "tenants";

    public static DocumentIds Ids => DocumentIds.MatchesRoute;

    public static JsonApiFailure IdMismatch { get; } =
        new(StatusCodes.Status409Conflict, "tenant-id-mismatch", "The id of the resource is not the one of the route.");

    /// <summary>The document of the rules, when it names them.</summary>
    public JsonElement? Rules =>
        (Members ?? []).FirstOrDefault(x => string.Equals(x.Key, REGIONAL_RULES, StringComparison.OrdinalIgnoreCase))
            is { Key: not null } named
            ? named.Value
            : null;

    public override JsonApiFailure? Invalid()
    {
        var members = Members ?? [];
        if (members.Keys.Any(x => !string.Equals(x, REGIONAL_RULES, StringComparison.OrdinalIgnoreCase)))
        {
            return new JsonApiFailure(
                StatusCodes.Status400BadRequest,
                "unsupported-attribute",
                "Only the rules of the Regional competitions of a Tenant can be changed.",
                REGIONAL_RULES
            );
        }

        // What is named is valid whatever the Tenant has now: only the members that are not named come from it.
        return Rules is { } named && Merge(new RegionalRules(false, null), named) is null
            ? new JsonApiFailure(
                StatusCodes.Status400BadRequest,
                "invalid-rules",
                "The rules are onlyAverageLoopSpeed (true or false) and rankerCode (a short text, or null for none)."
            )
            : null;
    }
}

/// <summary>The change of the Tenant an account acts in: the only member of the account that this resource takes.</summary>
internal sealed class SelectionChange : MemberAttributes, IResourceAttributes
{
    const string SELECTED_TENANT_ID = "selectedTenantId";

    public static string? ResourceType => "accounts";

    /// <summary>The Tenant the document chooses, or null to go back to the home Tenant; none when the document does not name it.</summary>
    public JsonElement? Choice =>
        (Members ?? []).FirstOrDefault(x =>
            string.Equals(x.Key, SELECTED_TENANT_ID, StringComparison.OrdinalIgnoreCase)
        )
            is { Key: not null } named
            ? named.Value
            : null;

    public override JsonApiFailure? Invalid()
    {
        if ((Members ?? []).Keys.Any(x => !string.Equals(x, SELECTED_TENANT_ID, StringComparison.OrdinalIgnoreCase)))
        {
            return new JsonApiFailure(
                StatusCodes.Status400BadRequest,
                "unsupported-attribute",
                "Only the selected Tenant of an account can be changed.",
                SELECTED_TENANT_ID
            );
        }

        return Choice is { ValueKind: not (JsonValueKind.Null or JsonValueKind.String) }
            ? new JsonApiFailure(
                StatusCodes.Status400BadRequest,
                "malformed-request",
                "The selected Tenant is the id of a Tenant, or null to go back to the home Tenant."
            )
            : null;
    }
}
