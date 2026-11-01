
namespace NoTiming.Api.Features.Account;

internal sealed class RegistrationAttributes
{
    public string? Email { get; set; }
    public string? GivenName { get; set; }
    public string? Surname { get; set; }

    /// <summary>The id of one of the countries the page lists, as the page sends it.</summary>
    public string? CountryId { get; set; }

    /// <summary>The field of the page that people do not see: it is empty unless a program filled it in.</summary>
    public string? Website { get; set; }
}
