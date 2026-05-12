using Not.Application.Authentication.User;
using NTS.Domain.Aggregates;
using NTS.Witness.Contracts.API;

namespace NTS.Witness.Contracts.Features.Profile;

public class WitnessProfileFormModel
{
    public static WitnessProfileFormModel From(NUserModel? user, Country? country)
    {
        return new WitnessProfileFormModel
        {
            GivenName = user?.GivenName,
            MiddleName = user?.MiddleName,
            Surname = user?.Surname,
            Country = country,
            Club = user?.Club,
            FeiId = user?.FeiId,
        };
    }

    public string? GivenName { get; set; }
    public string? MiddleName { get; set; }
    public string? Surname { get; set; }
    public Country? Country { get; set; }
    public string? Club { get; set; }
    public string? FeiId { get; set; }

    public UpdateUserProfilePayload ToPayload()
    {
        return new UpdateUserProfilePayload(GivenName, Surname, Country?.Name, MiddleName, Club, FeiId);
    }
}
