using MongoDB.Bson;

namespace NC.Server.Authentication.Codes;

/// <summary>The details a registration keeps until its code is entered, or until the challenge expires.</summary>
internal sealed class RegistrationDetailsDocument
{
    public string GivenName { get; set; } = string.Empty;

    public string Surname { get; set; } = string.Empty;

    /// <summary>The app's profile, with the names the API uses. The module does not read its fields.</summary>
    public BsonDocument? Profile { get; set; }
}
