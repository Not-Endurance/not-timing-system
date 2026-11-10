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

    /// <summary>The profile stored in the document, or an empty profile when there is none.</summary>
    public static TProfile FromBson<TProfile>(BsonDocument? document)
        where TProfile : class, new()
    {
        if (document is null)
        {
            return new TProfile();
        }

        return JsonSerializer.Deserialize<TProfile>(document.ToJson(), Options) ?? new TProfile();
    }
}
