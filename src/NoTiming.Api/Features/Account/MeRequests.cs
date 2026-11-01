using System.Text.Json;
using NoTiming.Api.Features.Access;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Account;

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
            return AccountFailures.MeUnsupportedAttribute.With(SELECTED_TENANT_ID);
        }

        return Choice is { ValueKind: not (JsonValueKind.Null or JsonValueKind.String) }
            ? AccountFailures.MeMalformedRequest
            : null;
    }
}
