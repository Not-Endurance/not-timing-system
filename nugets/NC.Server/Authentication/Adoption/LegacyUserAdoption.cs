using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using NC.Server.Authentication.Identity;

namespace NC.Server.Authentication.Adoption;

/// <summary>
/// The transitional step that keeps existing users signed in. A row from before identity has no proof of its address:
/// when a code for its address is verified, the row is adopted in place, keeping its integer id and its roles, so the
/// event roles that reference it keep working. The verification path calls this through
/// <see cref="ILegacyUserAdoption"/>, and nothing else depends on it, so it is removed by deleting this class and its
/// registration.
/// </summary>
internal sealed class LegacyUserAdoption : ILegacyUserAdoption
{
    readonly IIdentityUserRepository _users;
    readonly ILogger<LegacyUserAdoption> _logger;

    public LegacyUserAdoption(IIdentityUserRepository users, ILogger<LegacyUserAdoption> logger)
    {
        _users = users;
        _logger = logger;
    }

    public async Task<IdentityUserDocument?> Adopt(
        IdentityUserDocument legacy,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        legacy.Email = EmailAddress.Normalize(legacy.Email ?? string.Empty);
        legacy.ProvedAtUtc = now.UtcDateTime;
        legacy.UserHandle ??= RandomNumberGenerator.GetBytes(32);
        legacy.SecurityStamp ??= Guid.NewGuid().ToString("N");

        var result = await _users.UpdateAsync(legacy, cancellationToken);
        if (!result.Succeeded)
        {
            _logger.LogWarning("A legacy user could not be adopted: the update was refused.");
            return null;
        }

        _logger.LogInformation("A legacy user {UserId} proved the address by code and was adopted.", legacy.Id);
        return legacy;
    }
}
