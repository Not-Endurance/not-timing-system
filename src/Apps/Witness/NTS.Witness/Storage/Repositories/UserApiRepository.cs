using Not.Application.Authentication.Abstractions;
using Not.Application.Authentication.User;
using Not.Application.HTTP;
using Not.Notify;
using Not.Structures;
using NTS.Witness.Contracts.API;

namespace NTS.Witness.Storage.Repositories;

public class UserApiRepository : IUserRegister
{
    readonly NHttpClient _client;

    public UserApiRepository(NHttpClient client)
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
            return await _client.Get<NUserModel>($"users/{encodedEmail}");
        }
        catch (Exception ex)
        {
            NotificationHelper.Current?.Error(ex);
            return Result.Failure<NUserModel>(ex.Message);
        }
    }

    public async Task<Result<NUserModel>> Register(NUserRegistration registration)
    {
        if (string.IsNullOrWhiteSpace(registration.Email))
        {
            return Result.Cancel<NUserModel>();
        }

        try
        {
            return await _client.Post<NUserModel>(
                "users/register",
                new RegisterUserPaload(
                    registration.Email,
                    registration.Name,
                    registration.GivenName,
                    registration.Surname,
                    registration.CountryRegion,
                    registration.MiddleName,
                    registration.Club,
                    registration.FeiId
                )
            );
        }
        catch (Exception ex)
        {
            NotificationHelper.Current?.Error(ex);
            return Result.Failure<NUserModel>(ex.Message);
        }
    }
}
