using Not.Application.Authentication.User;
using Not.Structures;
using NTS.Witness.Contracts.API;

namespace NTS.Witness.Features.Profile;

public interface IWitnessUserProfileRepository
{
    Task<Result<NUserModel>> UpdateProfile(string email, UpdateUserProfilePayload payload);
}
