using Not.Application.Authentication.User;
using Not.Structures;

namespace Not.Application.Authentication.Abstractions;

public interface IUserRegister
{
    Task<Result<NUserModel>> Get(string email);
    Task<Result<NUserModel>> Register(NUserRegistration registration);
}
