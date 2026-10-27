using System.Text.Json;
using System.Text.Json.Serialization;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Account;
using NoTiming.Api.Features.Participants;
using NoTiming.Api.JsonApi;
using NTS.Contracts.API;
using NTS.Domain.Aggregates;

namespace NoTiming.Api.Features.Profile;

/// <summary>
/// The members of a profile that a PATCH names. They are kept as they were sent, because "not named" and "null" mean
/// different things: the first leaves a member alone and the second clears it.
/// </summary>
internal sealed class ProfileAttributes : IResourceAttributes
{
    public static string? ResourceType => "profiles";

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Members { get; set; }

    /// <summary>The members are checked against the account they are merged into, which only the handler has.</summary>
    public JsonApiFailure? Invalid()
    {
        return null;
    }
}
