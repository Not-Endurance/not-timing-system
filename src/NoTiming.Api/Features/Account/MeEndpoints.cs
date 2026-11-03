using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Not.Identity;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Profile;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// Who is signed in (ADR-0002), and the Tenant the account acts in (ADR-0012): the account chooses the Tenant that what it
/// reads and writes belongs to from among the ones it is a member of. It acts on the caller and on nobody else. Nothing here
/// gives a role: roles are written by the Developer's command alone.
/// </summary>
internal static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMe(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", GetMe);
        app.MapPatch("/api/me", PatchMe);
        return app;
    }

    // What an account does for itself is any signed-in account's.
    [Requires(Capability.SignedIn)]
    static IResult GetMe(AccessContext access)
    {
        return MeResource.Of(access.User, StatusCodes.Status200OK);
    }

    [Requires(Capability.SignedIn)]
    static async Task<IResult> PatchMe(
        JsonApiBody<SelectionChange> body,
        AccessContext access,
        UserManager<NIdentityUser> users,
        ProfileStore store,
        TenantStore tenants,
        CancellationToken cancellationToken
    )
    {
        var user = access.User;
        if (body.Id != null && body.Id != user.Id.ToString())
        {
            return AccountFailures.AccountIdMismatch.AsResult();
        }

        if (body.Attributes.Choice is { } choice)
        {
            var tenantId = choice.ValueKind == JsonValueKind.String ? choice.GetString() : null;
            if (tenantId != null && !await MayActIn(user, tenantId, tenants, cancellationToken))
            {
                return AccountFailures.NotAMember.AsResult();
            }

            await store.SaveAsync(
                user.Id,
                new Dictionary<string, string?> { [AccountRoles.SELECTED_TENANT] = tenantId },
                cancellationToken
            );
        }

        var saved = (await users.FindByIdAsync(user.Id.ToString()))!;
        return MeResource.Of(saved, StatusCodes.Status200OK);
    }

    /// <summary>
    /// Whether the account may select the Tenant: it is a member of it, or it is the Developer and there is such a Tenant.
    /// </summary>
    static async Task<bool> MayActIn(
        NIdentityUser user,
        string tenantId,
        TenantStore tenants,
        CancellationToken cancellationToken
    )
    {
        return AccountRoles.MembershipsOf(user).Any(x => x.TenantId == tenantId)
            || (AccountRoles.IsDeveloper(user) && await tenants.FindAsync(tenantId, cancellationToken) is not null);
    }
}
