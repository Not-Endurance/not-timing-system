using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using NoTiming.Api.Application.Authentication.Store;

namespace NoTiming.Api.Application.Authentication.Adoption;

/// <summary>
/// The transitional step that keeps existing users signed in. A row from before identity has no proof of its address:
/// when a code for its address is verified, the row is adopted in place, keeping its integer id and its roles, so the
/// event roles that reference it keep working. The verification path calls this through
/// <see cref="ILegacyUserAdoption"/>, and nothing else depends on it, so it is removed by deleting this class and its
/// registration.
/// </summary>
public sealed class LegacyUserAdoption : ILegacyUserAdoption
{
    readonly IdentityUserStore _users;
    readonly ILogger<LegacyUserAdoption> _logger;

    public LegacyUserAdoption(IdentityUserStore users, ILogger<LegacyUserAdoption> logger)
    {
        _users = users;
        _logger = logger;
    }

    public async Task<IdentityUserDocument?> AdoptAsync(IdentityUserDocument legacy, DateTimeOffset now, CancellationToken cancellationToken)
    {
        legacy.Email = Identity.EmailAddress.Normalize(legacy.Email ?? string.Empty);
        legacy.ProvedAt = now.UtcDateTime;
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

/// <summary>Adopts a legacy user row when its address is proved. Several implementations may be registered; the first to adopt wins.</summary>
public interface ILegacyUserAdoption
{
    Task<IdentityUserDocument?> AdoptAsync(IdentityUserDocument legacy, DateTimeOffset now, CancellationToken cancellationToken);
}
