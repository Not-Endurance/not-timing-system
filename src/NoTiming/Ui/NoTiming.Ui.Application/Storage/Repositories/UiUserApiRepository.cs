using NC.Application.Authentication.Abstractions;
using NC.Application.Authentication.User;
using NC.Application.HTTP;
using NC.Notify;
using NC.Structures;
using NoTiming.Ui.Contracts.API;
using NoTiming.Ui.Application.Features.Profile;

namespace NoTiming.Ui.Application.Storage.Repositories;

public class UiUserApiRepository : IUserRegister, IUiUserProfileRepository
{
    readonly NHttpClient _client;

    public UiUserApiRepository(NHttpClient client)
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
                new RegisterUserPayload(
                    registration.Email,
                    registration.Name,
                    registration.GivenName,
                    registration.Surname,
                    registration.CountryRegion,
                    registration.MiddleName,
                    registration.Club,
                    registration.FeiId,
                    registration.DisplayName
                )
            );
        }
        catch (Exception ex)
        {
            NotificationHelper.Current?.Error(ex);
            return Result.Failure<NUserModel>(ex.Message);
        }
    }

    public async Task<Result<NUserModel>> UpdateProfile(string email, UpdateUserProfilePayload payload)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Cancel<NUserModel>();
        }

        try
        {
            var encodedEmail = Uri.EscapeDataString(email);
            return await _client.Patch<NUserModel>($"users/{encodedEmail}/profile", payload);
        }
        catch (Exception ex)
        {
            NotificationHelper.Current?.Error(ex);
            return Result.Failure<NUserModel>(ex.Message);
        }
    }
}
