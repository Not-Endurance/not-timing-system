using System.Text.Json;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Profile;
using NoTiming.Api.JsonApi;
using NTS.Domain.Aggregates;
using NTS.Domain.Objects;

namespace NoTiming.Api.Features.Tenancy;

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
