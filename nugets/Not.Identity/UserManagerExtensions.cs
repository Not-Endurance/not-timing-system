using Microsoft.AspNetCore.Identity;

namespace Not.Identity;

public static class UserManagerExtensions
{
    const int ATTEMPTS = 4;

    /// <summary>
    /// Loads the user, applies a change and saves it, and when another request changed the same user in between (the
    /// update names the concurrency stamp it was based on, so that one is refused) starts again from what is stored now.
    /// Two changes made at the same moment, two passkeys enrolled at once for example, both end up stored.
    /// </summary>
    public static async Task<IdentityResult> ChangeAsync<TUser>(
        this UserManager<TUser> users,
        Guid userId,
        Func<TUser, Task<IdentityResult>> change
    )
        where TUser : class
    {
        var result = IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure());
        for (var attempt = 0; attempt < ATTEMPTS; attempt++)
        {
            var user = await users.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return IdentityResult.Failed(
                    new IdentityError { Code = "UserNotFound", Description = "There is no such user." }
                );
            }

            result = await change(user);
            if (result.Succeeded || result.Errors.All(x => x.Code != nameof(IdentityErrorDescriber.ConcurrencyFailure)))
            {
                return result;
            }
        }

        return result;
    }
}
