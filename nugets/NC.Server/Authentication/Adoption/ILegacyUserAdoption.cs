using NC.Server.Authentication.Identity;

namespace NC.Server.Authentication.Adoption;

/// <summary>Adopts a legacy user row when its address is proved. Several implementations may be registered; the first to adopt wins.</summary>
public interface ILegacyUserAdoption
{
    Task<IdentityUserDocument?> Adopt(IdentityUserDocument legacy, DateTimeOffset now, CancellationToken cancellationToken);
}
