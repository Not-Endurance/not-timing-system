using NC.Application.Authentication.User;
using NC.Structures;

namespace NC.Application.Authentication.Abstractions;

/// <summary>Reads the profile of the signed-in person. Accounts are created by sign-in, not here.</summary>
public interface IUserRegister
{
    Task<Result<NUserModel>> Get(string email);
}
