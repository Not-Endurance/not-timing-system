using NC.Server.Authentication.Profiles;

namespace NC.Server.Authentication.Account;

/// <summary>The profile of a signed-in user, read and changed by the module's account routes.</summary>
internal sealed class AccountProfiles<TProfile>
    where TProfile : class, new()
{
    readonly AccountService _account;

    public AccountProfiles(AccountService account)
    {
        _account = account;
    }

    public async Task<AuthenticatedUser<TProfile>?> Find(int userId, CancellationToken cancellationToken)
    {
        var user = await _account.Find(userId, cancellationToken);
        return user is null ? null : AuthenticatedUser<TProfile>.From(user);
    }

    public async Task<ProfileUpdateOutcome> Update(
        int userId,
        ProfileChanges<TProfile> changes,
        CancellationToken cancellationToken
    )
    {
        var saved = await _account.UpdateProfile(
            userId,
            changes.GivenName,
            changes.MiddleName,
            changes.Surname,
            ProfileDocument.ToBson(changes.Profile),
            cancellationToken
        );
        return saved ? ProfileUpdateOutcome.Saved : ProfileUpdateOutcome.MissingDetails;
    }
}
