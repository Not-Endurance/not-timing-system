using NC.Server.Authentication.Identity;
using NC.Server.Authentication.Sessions;

namespace NC.Server.Authentication.Account;

/// <summary>The signed-in user's own state: who they are, their profile and ending their session.</summary>
public sealed class AccountService
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

    /// <summary>Returns false when the required details are missing: the country and both names.</summary>
    public async Task<bool> UpdateProfile(int userId, ProfileChanges changes, CancellationToken cancellationToken)
    {
        if (
            string.IsNullOrWhiteSpace(changes.CountryRegion)
            || string.IsNullOrWhiteSpace(changes.GivenName)
            || string.IsNullOrWhiteSpace(changes.Surname)
        )
        {
            return false;
        }

        await _users.UpdateProfile(
            userId,
            changes.GivenName.Trim(),
            changes.MiddleName?.Trim(),
            changes.Surname.Trim(),
            changes.CountryRegion.Trim(),
            changes.Club?.Trim(),
            changes.FeiId?.Trim(),
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
