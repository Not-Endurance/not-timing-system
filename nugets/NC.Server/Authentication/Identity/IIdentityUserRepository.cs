using Microsoft.AspNetCore.Identity;
using MongoDB.Bson;

namespace NC.Server.Authentication.Identity;

/// <summary>
/// The users sign-in reads and changes, and the passkeys they hold. The Identity store implements it over the <c>users</c>
/// collection.
/// </summary>
internal interface IIdentityUserRepository
{
    Task<IdentityUserDocument?> FindByUserId(int userId, CancellationToken cancellationToken = default);

    Task<List<IdentityUserDocument>> FindAll(CancellationToken cancellationToken);

    Task<List<IdentityUserDocument>> FindAllByEmail(string normalizedEmail, CancellationToken cancellationToken);

    Task<IdentityUserDocument> CreateProvedUser(
        string email,
        string givenName,
        string surname,
        BsonDocument? profile,
        DateTimeOffset now,
        CancellationToken cancellationToken
    );

    Task UpdateProfile(
        int userId,
        string givenName,
        string? middleName,
        string surname,
        BsonDocument profile,
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

    /// <summary>Records that a passkey signed the user in at <paramref name="usedAt"/>, on the user the change is working on.</summary>
    Task SetPasskeyLastUsed(
        IdentityUserDocument user,
        byte[] credentialId,
        DateTimeOffset usedAt,
        CancellationToken cancellationToken
    );

    /// <summary>Removes one of the user's passkeys. False when the user has no passkey with that id.</summary>
    Task<bool> RemovePasskey(int userId, byte[] credentialId, CancellationToken cancellationToken = default);

    /// <summary>Renames one of the user's passkeys. False when the user has no passkey with that id.</summary>
    Task<bool> RenamePasskey(
        int userId,
        byte[] credentialId,
        string name,
        CancellationToken cancellationToken = default
    );
}
