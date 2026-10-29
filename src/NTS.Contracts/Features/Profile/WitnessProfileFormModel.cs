using NTS.Domain.Aggregates;

namespace NTS.Contracts.Features.Profile;

public class WitnessProfileFormModel
{
    public static WitnessProfileFormModel From(AccountProfile? profile, Country? country)
    {
        return new WitnessProfileFormModel
        {
            GivenName = profile?.GivenName,
            MiddleName = profile?.MiddleName,
            Surname = profile?.Surname,
            Country = country,
            Club = profile?.Club,
            FeiId = profile?.FeiId,
        };
    }

    public string? GivenName { get; set; }
    public string? MiddleName { get; set; }
    public string? Surname { get; set; }
    public Country? Country { get; set; }
    public string? Club { get; set; }
    public string? FeiId { get; set; }
}
