namespace NC.Server.Authentication.Interface;

/// <summary>
/// The users, with their profiles, for the app's own lookups: the directory of users and the receive authorization. The
/// module owns the users; the app reads them through this, never from the collection.
/// </summary>
public interface IUserDirectory<TProfile>
    where TProfile : class, new()
{
    Task<IReadOnlyList<AuthenticatedUser<TProfile>>> ReadAll(CancellationToken cancellationToken = default);

    /// <summary>The user with the address, or null when there is none or more than one.</summary>
    Task<AuthenticatedUser<TProfile>?> FindByEmail(string email, CancellationToken cancellationToken = default);
}
