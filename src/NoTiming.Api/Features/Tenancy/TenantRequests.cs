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

    public static JsonApiFailure IdMismatch => TenancyFailures.TenantIdMismatch;

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
            return TenancyFailures.UnsupportedAttribute.With(REGIONAL_RULES);
        }

        // What is named is valid whatever the Tenant has now: only the members that are not named come from it.
        return Rules is { } named && Merge(new RegionalRules(false, null), named) is null
            ? TenancyFailures.InvalidRules
            : null;
    }
}
