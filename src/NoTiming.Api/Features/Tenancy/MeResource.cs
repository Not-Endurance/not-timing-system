using Not.Identity;
using NoTiming.Api.Features.Profile;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>
/// The <c>accounts</c> resource of the caller, as <c>GET /api/me</c> and <c>PATCH /api/me</c> answer it: who they are, and
/// where they stand in the Tenants (ADR-0012): the home Tenant, the Tenant they have selected, the Tenant that what they
/// read and write belongs to, their Memberships with the roles in them, and whether they are the Developer. The roles are
/// what the Ui needs to decide what to offer; the server decides what is allowed.
/// </summary>
internal static class MeResource
{
    public static IResult Of(NIdentityUser user, int status)
    {
        return JsonApiResults.Resource(
            status,
            "accounts",
            user.Id.ToString(),
            new
            {
                email = user.Email,
                emailConfirmed = user.EmailConfirmed,
                name = user.TextOf("Name"),
                passkeys = user.Passkeys.Count,
                profileComplete = ProfileEndpoints.IsComplete(user),
                homeTenantId = AccountRoles.HomeTenantOf(user),
                selectedTenantId = AccountRoles.SelectedTenantOf(user),
                currentTenantId = AccountRoles.CurrentTenantOf(user),
                memberships = AccountRoles
                    .MembershipsOf(user)
                    .Select(x => new { tenantId = x.TenantId, roles = x.Roles }),
                isDeveloper = AccountRoles.IsDeveloper(user),
            }
        );
    }
}
