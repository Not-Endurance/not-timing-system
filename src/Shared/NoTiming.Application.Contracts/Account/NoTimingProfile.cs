namespace NoTiming.Application.Contracts.Account;

/// <summary>
/// NoTiming's profile fields, stored with each user by the Authentication module. The club and federation id are optional;
/// the country is required to register (see the profile validator).
/// </summary>
public sealed class NoTimingProfile
{
    public string? Club { get; set; }

    public string? FeiId { get; set; }

    public string? CountryIsoCode { get; set; }
}
