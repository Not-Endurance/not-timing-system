using NC.Application.Authentication.User;
using NC.Structures;

namespace NC.Application.Authentication.Abstractions;

public interface IUserRegister
{
    Task<Result<NUserModel>> Get(string email);
    Task<Result<NUserModel>> Register(NUserRegistration registration);
}
