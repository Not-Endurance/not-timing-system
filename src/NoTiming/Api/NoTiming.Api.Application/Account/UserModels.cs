using NC.Server.Authentication;
using NoTiming.Application.Contracts.Account;
using NoTiming.Application.Contracts.Setup.Models;

namespace NoTiming.Api.Application.Account;

/// <summary>The directory's view of a user, in the shape Judge reads.</summary>
public static class UserModels
{
    public static UserModel From(AuthenticatedUser<NoTimingProfile> user)
    {
        return new UserModel
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            Name = user.Name ?? string.Empty,
            DisplayName = user.DisplayName,
            GivenName = user.GivenName,
            MiddleName = user.MiddleName,
            Surname = user.Surname,
            CountryIsoCode = user.Profile.CountryIsoCode,
            Club = user.Profile.Club,
            FeiId = user.Profile.FeiId,
            Roles = user.Roles,
        };
    }
}
