using NC.Server.Authentication.Interface;

namespace NC.Server.Authentication.Identity;

/// <summary>The app's view of the users, with their profiles.</summary>
internal sealed class UserDirectory<TProfile> : IUserDirectory<TProfile>
    where TProfile : class, new()
{
    readonly IIdentityUserRepository _users;

    public UserDirectory(IIdentityUserRepository users)
    {
        _users = users;
    }

    public async Task<IReadOnlyList<AuthenticatedUser<TProfile>>> ReadAll(
        CancellationToken cancellationToken = default
    )
    {
        var users = await _users.FindAll(cancellationToken);
        return [.. users.Select(AuthenticatedUser<TProfile>.From)];
    }

    public async Task<AuthenticatedUser<TProfile>?> FindByEmail(
        string email,
        CancellationToken cancellationToken = default
    )
    {
        var matches = await _users.FindAllByEmail(EmailAddress.Normalize(email), cancellationToken);
        return matches.Count == 1 ? AuthenticatedUser<TProfile>.From(matches[0]) : null;
    }
}
