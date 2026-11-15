using MongoDB.Bson;
using NC.Server.Authentication.Identity;
using NC.Server.Authentication.Sessions;

namespace NC.Server.Authentication.Account;

/// <summary>The signed-in user's own state: who they are, their profile and ending their session.</summary>
internal sealed class AccountService
{
    readonly IIdentityUserRepository _users;
    readonly ISessionRepository _sessions;

    public AccountService(IIdentityUserRepository users, ISessionRepository sessions)
    {
        _users = users;
        _sessions = sessions;
    }

    public Task<IdentityUserDocument?> Find(int userId, CancellationToken cancellationToken)
    {
        return _users.FindByUserId(userId, cancellationToken);
    }

    /// <summary>Returns false when the required details are missing: both names. The profile is the app's to check.</summary>
    public async Task<bool> UpdateProfile(
        int userId,
        string givenName,
        string? middleName,
        string surname,
        BsonDocument profile,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(givenName) || string.IsNullOrWhiteSpace(surname))
        {
            return false;
        }

        await _users.UpdateProfile(
            userId,
            givenName.Trim(),
            middleName?.Trim(),
            surname.Trim(),
            profile,
            cancellationToken
        );
        return true;
    }

    public Task SignOut(string? sessionToken, CancellationToken cancellationToken)
    {
        return string.IsNullOrEmpty(sessionToken)
            ? Task.CompletedTask
            : _sessions.Remove(SessionToken.HashOf(sessionToken), cancellationToken);
    }
}
