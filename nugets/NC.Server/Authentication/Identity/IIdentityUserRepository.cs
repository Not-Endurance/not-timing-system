using Microsoft.AspNetCore.Identity;

namespace NC.Server.Authentication.Identity;

/// <summary>
/// The users sign-in reads and changes, and the passkeys they hold. The Identity store implements it over the <c>users</c>
/// collection.
/// </summary>
public interface IIdentityUserRepository
{
    Task<IdentityUserDocument?> FindByUserId(int userId, CancellationToken cancellationToken = default);

    Task<List<IdentityUserDocument>> FindAllByEmail(string normalizedEmail, CancellationToken cancellationToken);

    Task<IdentityUserDocument> CreateProvedUser(
        string email,
        string givenName,
        string surname,
        string countryRegion,
        DateTimeOffset now,
        CancellationToken cancellationToken
    );

    Task UpdateProfile(
        int userId,
        string givenName,
        string? middleName,
        string surname,
        string countryRegion,
        string? club,
        string? feiId,
        CancellationToken cancellationToken
    );

    Task<IdentityResult> Change(
        int userId,
        Func<IdentityUserDocument, Task<IdentityResult>> change,
        CancellationToken cancellationToken = default
    );

    Task<IdentityResult> UpdateAsync(IdentityUserDocument user, CancellationToken cancellationToken);

    Task AddOrUpdatePasskeyAsync(
        IdentityUserDocument user,
        UserPasskeyInfo passkey,
        CancellationToken cancellationToken
    );

    Task<IList<UserPasskeyInfo>> GetPasskeysAsync(IdentityUserDocument user, CancellationToken cancellationToken);

    Task<bool> RemovePasskeyWhileAnotherRemains(
        int userId,
        byte[] credentialId,
        CancellationToken cancellationToken = default
    );
}
