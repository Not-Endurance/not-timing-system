using NC.Application.Authentication.User;
using NoTiming.Application.Contracts.Account;
using NoTiming.Domain.Aggregates;
using NoTiming.Ui.Contracts.API;

namespace NoTiming.Ui.Contracts.Features.Profile;

public class UiProfileFormModel
{
    public static UiProfileFormModel From(NUserModel? user, NoTimingProfile? profile, Country? country)
    {
        return new UiProfileFormModel
        {
            GivenName = user?.GivenName,
            MiddleName = user?.MiddleName,
            Surname = user?.Surname,
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

    public UpdateUserProfilePayload ToPayload()
    {
        return new UpdateUserProfilePayload(GivenName, Surname, Country?.Name, MiddleName, Club, FeiId);
    }
}
