using System.Net;
using NC.Application.Authentication.Abstractions;
using NC.Application.Authentication.User;
using NC.Application.HTTP;
using NC.Notify;
using NC.Serialization.JSON;
using NC.Structures;
using NoTiming.Application.Contracts.Account;
using NoTiming.Ui.Application.Features.Profile;
using NoTiming.Ui.Contracts.API;
using NoTiming.Ui.Contracts.Features.Profile;

namespace NoTiming.Ui.Application.Storage.Repositories;

/// <summary>The profile of the signed-in person, read and changed through the session. There is no lookup by address.</summary>
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
            var json = (await _client.GetJson(PROFILE_ENDPOINT)).Data!;
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

    public async Task<Result<UiSignedInProfile>> Read(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Cancel<UiSignedInProfile>();
        }

        try
        {
            var json = (await _client.GetJson(PROFILE_ENDPOINT)).Data!;
            var profile = json.FromJson<SignedInProfile>();
            if (!string.Equals(profile.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<UiSignedInProfile>("The signed-in profile belongs to another address.");
            }

            return Result.Success(new UiSignedInProfile(profile.ToModel(), profile.Profile ?? new NoTimingProfile()));
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            return Result.Failure<UiSignedInProfile>("The session has ended.");
        }
        catch (Exception ex)
        {
            NotificationHelper.Current?.Error(ex);
            return Result.Failure<UiSignedInProfile>(ex.Message);
        }
    }

    public async Task<Result<UiSignedInProfile>> UpdateProfile(string email, UpdateUserProfilePayload payload)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Cancel<UiSignedInProfile>();
        }

        try
        {
            await _client.PatchJson(PROFILE_UPDATE_ENDPOINT, payload.ToJson());
            return await Read(email);
        }
        catch (Exception ex)
        {
            NotificationHelper.Current?.Error(ex);
            return Result.Failure<UiSignedInProfile>(ex.Message);
        }
    }

    /// <summary>The identity and profile the server returns from the profile route.</summary>
    sealed class SignedInProfile
    {
        public int Id { get; set; }

        public string Email { get; set; } = string.Empty;

        public string? Name { get; set; }

        public string? GivenName { get; set; }

        public string? MiddleName { get; set; }

        public string? Surname { get; set; }

        public string[] Roles { get; set; } = [];

        public NoTimingProfile? Profile { get; set; }

        public NUserModel ToModel()
        {
            return new NUserModel(Email, Roles, Id)
            {
                Name = Name,
                GivenName = GivenName,
                MiddleName = MiddleName,
                Surname = Surname,
            };
        }
    }
}
