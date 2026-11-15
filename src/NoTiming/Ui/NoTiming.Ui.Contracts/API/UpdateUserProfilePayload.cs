using NoTiming.Application.Contracts.Account;

namespace NoTiming.Ui.Contracts.API;

public record UpdateUserProfilePayload
{
    public UpdateUserProfilePayload(
        string? givenName,
        string? surname,
        string? countryIsoCode,
        string? middleName = null,
        string? club = null,
        string? feiId = null
    )
    {
        GivenName = Normalize(givenName);
        MiddleName = Normalize(middleName);
        Surname = Normalize(surname);
        Profile = new NoTimingProfile
        {
            Club = Normalize(club),
            FeiId = Normalize(feiId),
            CountryIsoCode = Normalize(countryIsoCode),
        };
        Name = BuildName(GivenName, MiddleName, Surname);
    }

    public string? Name { get; }
    public string? GivenName { get; }
    public string? MiddleName { get; }
    public string? Surname { get; }
    public NoTimingProfile Profile { get; }

    public bool HasRequiredProfile()
    {
        return !string.IsNullOrWhiteSpace(GivenName)
            && !string.IsNullOrWhiteSpace(Surname)
            && !string.IsNullOrWhiteSpace(Profile.CountryIsoCode);
    }

    static string? BuildName(params string?[] parts)
    {
        var nameParts = parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToArray();
        return nameParts.Length == 0 ? null : string.Join(" ", nameParts);
    }

    static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
