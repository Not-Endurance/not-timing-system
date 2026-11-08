using NC.Server.Authentication.Sessions;
using NC.Server.Authentication.Store;

namespace NC.Server.Authentication.Account;

/// <summary>The signed-in user's own state: who they are, their profile and ending their session.</summary>
public sealed class AccountService
{
    readonly IdentityUserStore _users;
    readonly SessionStore _sessions;

    public AccountService(IdentityUserStore users, SessionStore sessions)
    {
        _users = users;
        _sessions = sessions;
    }

    public Task<IdentityUserDocument?> FindAsync(int userId, CancellationToken cancellationToken)
    {
        return _users.FindByUserIdAsync(userId, cancellationToken);
    }

    /// <summary>Returns false when the required details are missing: the country and both names.</summary>
    public async Task<bool> UpdateProfileAsync(int userId, ProfileChanges changes, CancellationToken cancellationToken)
    {
        if (
            string.IsNullOrWhiteSpace(changes.CountryRegion)
            || string.IsNullOrWhiteSpace(changes.GivenName)
            || string.IsNullOrWhiteSpace(changes.Surname)
        )
        {
            return false;
        }

        await _users.UpdateProfileAsync(
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

    public Task SignOutAsync(string? sessionToken, CancellationToken cancellationToken)
    {
        return string.IsNullOrEmpty(sessionToken)
            ? Task.CompletedTask
            : _sessions.RemoveAsync(SessionToken.HashOf(sessionToken), cancellationToken);
    }
}
