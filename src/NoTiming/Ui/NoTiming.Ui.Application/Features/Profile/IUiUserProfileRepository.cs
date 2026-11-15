using NC.Structures;
using NoTiming.Ui.Contracts.API;
using NoTiming.Ui.Contracts.Features.Profile;

namespace NoTiming.Ui.Application.Features.Profile;

public interface IUiUserProfileRepository
{
    Task<Result<UiSignedInProfile>> Read(string email);

    Task<Result<UiSignedInProfile>> UpdateProfile(string email, UpdateUserProfilePayload payload);
}
