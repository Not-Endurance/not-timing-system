using System.Text.Json;
using System.Text.Json.Serialization;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Access;

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

/// <summary>The document of a row that is made of the members it names: the id of it is the one the client made for the row, a Guid, and may be left out.</summary>
internal class NewMembers : MemberAttributes, IResourceAttributes
{
    public static DocumentIds Ids => DocumentIds.OptionalGuid;
}

/// <summary>The document of a change of a row: the members it names, and the id of the row, which it may give.</summary>
internal sealed class MemberChange : MemberAttributes, IResourceAttributes
{
    public static DocumentIds Ids => DocumentIds.MatchesRoute;
}
