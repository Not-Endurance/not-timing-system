using System.Text.Json;
using System.Text.Json.Serialization;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Account;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Domain.Access;
using NTS.Domain.Enums;

namespace NoTiming.Api.Features.Events;

/// <summary>
/// What a grant is sent as: an Event, a kind, a role for an Official, and a person by an email or an account. A member that
/// is not one of these is kept, to be refused.
/// </summary>
internal sealed class GrantAttributes : IResourceAttributes, INamesEvent
{
    public static string? ResourceType => "event-grants";

    public string? EventId { get; set; }
    public string? Kind { get; set; }
    public string? OfficialRole { get; set; }
    public string? Email { get; set; }
    public Guid? AccountId { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Others { get; set; }

    /// <summary>The kind of grant, once the attributes are valid.</summary>
    [JsonIgnore]
    public GrantKind GrantKind =>
        Kind!.Trim().ToLowerInvariant() == "official" ? GrantKind.Official : GrantKind.Operator;

    /// <summary>The role of the Official, once the attributes are valid; none for an Operator.</summary>
    [JsonIgnore]
    public OfficialRole? Role => GrantKind == GrantKind.Official ? Enum.Parse<OfficialRole>(OfficialRole!) : null;

    public JsonApiFailure? Invalid()
    {
        if (Others is { Count: > 0 })
        {
            return new JsonApiFailure(
                StatusCodes.Status400BadRequest,
                "unsupported-attribute",
                "A grant names an Event, a kind, a role for an Official, and a person by an email or an account.",
                string.Join(", ", Others.Keys)
            );
        }

        var kind = Kind?.Trim().ToLowerInvariant();
        if (kind is not ("operator" or "official"))
        {
            return Failure("invalid-kind", "A grant is for an operator or an official.");
        }

        if (kind == "official")
        {
            if (
                !Enum.TryParse<OfficialRole>(OfficialRole, ignoreCase: false, out var parsed) || !Enum.IsDefined(parsed)
            )
            {
                return Failure("invalid-role", "An official has a role of the domain, such as Steward.");
            }
        }
        else if (OfficialRole != null)
        {
            return Failure("invalid-role", "An operator has no role.");
        }

        if ((Email == null) == (AccountId == null))
        {
            return Failure("malformed-request", "Name the person by an email or by an account, and by one of them.");
        }

        return Email != null && !CodeSignIn.IsValidEmail(Email)
            ? Failure("invalid-email", "Enter a valid email address.")
            : null;
    }

    static JsonApiFailure Failure(string code, string title)
    {
        return new JsonApiFailure(StatusCodes.Status400BadRequest, code, title);
    }
}
