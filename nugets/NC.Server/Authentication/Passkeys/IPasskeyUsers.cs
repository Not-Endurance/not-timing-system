using Microsoft.AspNetCore.Identity;
using NC.Server.Authentication.Store;

namespace NC.Server.Authentication.Passkeys;

/// <summary>The stored users a passkey needs. The identity user store implements it.</summary>
public interface IPasskeyUsers
{
    Task<IdentityUserDocument?> FindByUserIdAsync(int userId, CancellationToken cancellationToken);

    Task<IdentityResult> ChangeAsync(
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

    Task<bool> RemovePasskeyWhileAnotherRemainsAsync(
        int userId,
        byte[] credentialId,
        CancellationToken cancellationToken = default
    );
}
