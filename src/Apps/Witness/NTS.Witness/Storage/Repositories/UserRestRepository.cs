using Not.Application.Authentication.Abstractions;
using Not.Application.Authentication.User;
using Not.Application.HTTP;
using Not.Injection;
using Not.Serialization.JSON;
using Not.Structures;
using NTS.Witness.Contracts.API;

namespace NTS.Witness.Storage.Repositories;

public class UserRestRepository : IUserRegister, ITransient
{
    readonly NHttpClient _client;

    public UserRestRepository(NHttpClient client)
    {
        _client = client;
    }

    public async Task<Result<NUserModel>> Get(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Cancel<NUserModel>();
        }

        try
        {
            var encodedEmail = Uri.EscapeDataString(email);
            var user = await _client.GetJson<NUserModel>($"users/{encodedEmail}");

            if (user == null)
            {
                return Result.Success<NUserModel>(null!);
            }

            return Result.Success(user);
        }
        catch (Exception ex)
        {
            return Result.Failure<NUserModel>(ex.Message);
        }
    }

    public async Task<Result<NUserModel>> Register(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Cancel<NUserModel>();
        }

        try
        {
            var response = await _client.Post("users/register", new RegisterUserPaload(email));
            var user = response.FromJson<NUserModel>();
            return Result.Success(user);
        }
        catch (Exception ex)
        {
            return Result.Failure<NUserModel>(ex.Message);
        }
    }
}
