using Not.Application.Authentication.User;
using Not.Structures;
using NoTiming.Ui.Contracts.API;

namespace NoTiming.Ui.Application.Features.Profile;

public interface IUiUserProfileRepository
{
    Task<Result<NUserModel>> UpdateProfile(string email, UpdateUserProfilePayload payload);
}
