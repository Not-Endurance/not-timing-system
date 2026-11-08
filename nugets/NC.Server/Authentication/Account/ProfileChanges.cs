namespace NC.Server.Authentication.Account;

public sealed class ProfileChanges
{
    public string GivenName { get; init; } = string.Empty;

    public string? MiddleName { get; init; }

    public string Surname { get; init; } = string.Empty;

    public string CountryRegion { get; init; } = string.Empty;

    public string? Club { get; init; }

    public string? FeiId { get; init; }
}
