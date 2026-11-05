using System.Net;
using NC.Application.Authentication.Abstractions;
using NC.Application.Authentication.User;
using NC.Application.HTTP;
using NC.Notify;
using NC.Serialization.JSON;
using NC.Structures;
using NoTiming.Ui.Application.Features.Profile;
using NoTiming.Ui.Contracts.API;

namespace NoTiming.Ui.Application.Storage.Repositories;

/// <summary>The signed-in person's own profile, read and changed through the session. There is no lookup by address.</summary>
public class UiUserApiRepository : IUserRegister, IUiUserProfileRepository
{
    const string PROFILE_ENDPOINT = "me/profile";
    const string PROFILE_UPDATE_ENDPOINT = "me";

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
            var json = await _client.SendRawJson(HttpMethod.Get, PROFILE_ENDPOINT);
            var profile = json.FromJson<SignedInProfile>();
            if (!string.Equals(profile.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<NUserModel>("The signed-in profile belongs to another address.");
            }

            return Result.Success(profile.ToModel());
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            return Result.Failure<NUserModel>("The session has ended.");
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
            await _client.SendRawJson(HttpMethod.Patch, PROFILE_UPDATE_ENDPOINT, payload.ToJson());
            return await Get(email);
        }
        catch (Exception ex)
        {
            NotificationHelper.Current?.Error(ex);
            return Result.Failure<NUserModel>(ex.Message);
        }
    }

    /// <summary>The identity the server returns from the profile route.</summary>
    sealed class SignedInProfile
    {
        public int Id { get; set; }

        public string Email { get; set; } = string.Empty;

        public string? Name { get; set; }

        public string? GivenName { get; set; }

        public string? MiddleName { get; set; }

        public string? Surname { get; set; }

        public string? CountryRegion { get; set; }

        public string? Club { get; set; }

        public string? FeiId { get; set; }

        public string[] Roles { get; set; } = [];

        public NUserModel ToModel()
        {
            return new NUserModel(Email, Roles, Id)
            {
                Name = Name,
                GivenName = GivenName,
                MiddleName = MiddleName,
                Surname = Surname,
                CountryRegion = CountryRegion,
                Club = Club,
                FeiId = FeiId,
            };
        }
    }
}
