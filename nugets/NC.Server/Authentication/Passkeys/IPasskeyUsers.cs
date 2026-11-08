using Microsoft.AspNetCore.Identity;
using NC.Server.Authentication.Identity;

namespace NC.Server.Authentication.Passkeys;

/// <summary>The stored users a passkey needs. The identity user repository implements it.</summary>
internal interface IPasskeyUsers
{
    Task<IdentityUserDocument?> FindByUserId(int userId, CancellationToken cancellationToken);

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
