using System.Text.Json;
using MongoDB.Bson;

namespace NC.Server.Authentication.Profiles;

/// <summary>
/// Stores the app's profile as a BSON document. The keys are the JSON names the API uses (camelCase), so the stored
/// profile and the profile the API returns have the same shape.
/// </summary>
internal static class ProfileDocument
{
    static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>The profile as a document. Text values are trimmed, as the names of the user are.</summary>
    public static BsonDocument ToBson<TProfile>(TProfile profile)
    {
        var document = BsonDocument.Parse(JsonSerializer.Serialize(profile, Options));
        foreach (var name in document.Names.ToList())
        {
            if (document[name].IsString)
            {
                document[name] = document[name].AsString.Trim();
            }
        }

        return document;
    }

    /// <summary>
    /// The profile stored in the document. An empty profile when there is none, or when the stored value cannot be read as
    /// the app's profile. The user keeps their identity, and the app asks for the profile again. One unreadable row does
    /// not fail the read of the others.
    /// </summary>
    public static TProfile FromBson<TProfile>(BsonValue? value)
        where TProfile : class, new()
    {
        if (value is not BsonDocument document)
        {
            return new TProfile();
        }

        try
        {
            return JsonSerializer.Deserialize<TProfile>(document.ToJson(), Options) ?? new TProfile();
        }
        catch (JsonException)
        {
            return new TProfile();
        }
    }
}
