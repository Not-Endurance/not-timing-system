using MongoDB.Bson;
using NTS.Domain.Aggregates;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// What a person typed to register (#601): their names and the country they chose. It waits in the code challenge until
/// the code that proves the address comes back, so nothing is stored for a person who never does.
/// </summary>
internal sealed class RegistrationDetails
{
    const int MAX_NAME_LENGTH = 100;
    const string TOKEN_HASH = "TokenHash";

    /// <summary>
    /// The details as the challenge holds them. None when the code came back without the secret of the browser that
    /// typed them (see <see cref="RegistrationTokens"/>), or when a name is missing or too long.
    /// </summary>
    public static async Task<RegistrationDetails?> ReadAsync(
        BsonDocument pending,
        SelectableCountries countries,
        string? presentedToken,
        CancellationToken cancellationToken
    )
    {
        var tokenHash = pending.TryGetValue(TOKEN_HASH, out var hash) && hash.IsString ? hash.AsString : null;
        if (!RegistrationTokens.Matches(presentedToken, tokenHash))
        {
            return null;
        }

        var givenName = TextOf(pending, "GivenName");
        var surname = TextOf(pending, "Surname");
        if (givenName == null || surname == null)
        {
            return null;
        }

        Country? country = null;
        if (
            pending.TryGetValue("CountryId", out var id)
            && id is BsonBinaryData { SubType: BsonBinarySubType.UuidStandard } binary
        )
        {
            country = await countries.FindAsync(binary.ToGuid(GuidRepresentation.Standard), cancellationToken);
        }

        return new RegistrationDetails(givenName, surname, country);
    }

    public static bool IsValidName(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= MAX_NAME_LENGTH && !name.Any(char.IsControl);
    }

    public RegistrationDetails(string givenName, string surname, Country? country)
    {
        GivenName = givenName.Trim();
        Surname = surname.Trim();
        Country = country;
    }

    public string GivenName { get; }
    public string Surname { get; }

    /// <summary>Null when the country was removed, or lost its ISO code, while the code was on its way.</summary>
    public Country? Country { get; }

    /// <param name="tokenHash">The hash of the secret of the browser that typed the details.</param>
    public BsonDocument ToPending(string tokenHash)
    {
        return new BsonDocument
        {
            ["GivenName"] = GivenName,
            ["Surname"] = Surname,
            ["CountryId"] = new BsonBinaryData(Country!.Id, GuidRepresentation.Standard),
            [TOKEN_HASH] = tokenHash,
        };
    }

    static string? TextOf(BsonDocument document, string field)
    {
        return document.TryGetValue(field, out var value) && value.IsString && IsValidName(value.AsString)
            ? value.AsString.Trim()
            : null;
    }
}
