using System.Text.Json;
using System.Text.Json.Serialization;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Profile;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Setup.Models;

namespace NoTiming.Api.Features.Events;

/// <summary>
/// What a new Event is made of: a name, a location and optionally an FEI show ID, and the id of the Setup the client made.
/// A member that is not one of these is kept, to be refused.
/// </summary>
internal sealed class NewEventAttributes : IResourceAttributes
{
    const int MAX_NAME_LENGTH = 100;
    const int MAX_FEI_SHOW_ID_LENGTH = 20;

    public static string? ResourceType => "configure-events";

    public static DocumentIds Ids => DocumentIds.OptionalGuid;

    public string? Name { get; set; }
    public string? Location { get; set; }
    public string? FeiShowId { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Others { get; set; }

    /// <summary>The name, once the attributes are valid.</summary>
    [JsonIgnore]
    public string NameText => Name!.Trim();

    /// <summary>The location, once the attributes are valid.</summary>
    [JsonIgnore]
    public string LocationText => Location!.Trim();

    /// <summary>The FEI show ID, once the attributes are valid; none when there is none.</summary>
    [JsonIgnore]
    public string? FeiShowIdText => string.IsNullOrWhiteSpace(FeiShowId) ? null : FeiShowId.Trim();

    public JsonApiFailure? Invalid()
    {
        if (Others is { Count: > 0 })
        {
            return new JsonApiFailure(
                StatusCodes.Status400BadRequest,
                "unsupported-attribute",
                "An Event is made with a name, a location and optionally an FEI show ID, and with nothing else.",
                string.Join(", ", Others.Keys)
            );
        }

        if (string.IsNullOrEmpty(Name?.Trim()) || !OneLineText.IsValid(NameText, MAX_NAME_LENGTH))
        {
            return new JsonApiFailure(
                StatusCodes.Status400BadRequest,
                "invalid-name",
                "An Event needs a name of one line of at most 100 characters."
            );
        }

        if (string.IsNullOrEmpty(Location?.Trim()) || !OneLineText.IsValid(LocationText, MAX_NAME_LENGTH))
        {
            return new JsonApiFailure(
                StatusCodes.Status400BadRequest,
                "invalid-location",
                "An Event needs a location of one line of at most 100 characters."
            );
        }

        return FeiShowIdText != null && !OneLineText.IsValid(FeiShowIdText, MAX_FEI_SHOW_ID_LENGTH)
            ? new JsonApiFailure(
                StatusCodes.Status400BadRequest,
                "invalid-fei-show-id",
                "An FEI show ID is at most 20 characters of one line."
            )
            : null;
    }
}
