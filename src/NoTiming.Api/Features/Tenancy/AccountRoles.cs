using MongoDB.Bson;
using Not.Identity;
using NTS.Domain.Access;
using NTS.Domain.Aggregates;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>
/// What a user document says about the account in the Tenants (ADR-0012): its home Tenant, its Memberships and their
/// roles, the Developer flag, and the Tenant it has selected. They are fields of the application's part of the document,
/// read as the Api wrote them. Only the Developer's command (<c>tools/NTS.Tools</c>) writes a role or the flag: no route
/// does. Reading is tolerant, as the documents are shared with the Functions API: a member that is not the shape it
/// should be is not there.
/// </summary>
internal static class AccountRoles
{
    public const string HOME_TENANT = "HomeTenantId";
    public const string SELECTED_TENANT = "SelectedTenantId";
    public const string MEMBERSHIPS = "Memberships";
    public const string IS_DEVELOPER = "IsDeveloper";

    /// <summary>
    /// The account as the access policy needs to know it. The Tenants of the Events it is the Main Operator of, which only
    /// some questions need, are read by whoever asks them and given here.
    /// </summary>
    public static Caller CallerOf(NIdentityUser user, IEnumerable<string>? openMainOperatorIn = null)
    {
        return Caller.Of(
            user.Id,
            IsDeveloper(user),
            MembershipsOf(user).Where(x => x.IsTenantRoot).Select(x => x.TenantId),
            openMainOperatorIn
        );
    }

    public static IReadOnlyList<Membership> MembershipsOf(NIdentityUser user)
    {
        if (user.OtherFields is null || !user.OtherFields.TryGetValue(MEMBERSHIPS, out var value) || !value.IsBsonArray)
        {
            return [];
        }

        var memberships = new List<Membership>();
        foreach (var element in value.AsBsonArray.Where(x => x.IsBsonDocument).Select(x => x.AsBsonDocument))
        {
            if (!element.TryGetValue("TenantId", out var tenant) || !tenant.IsString || tenant.AsString.Length == 0)
            {
                continue;
            }

            var roles =
                element.TryGetValue("Roles", out var held) && held.IsBsonArray
                    ? held.AsBsonArray.Where(x => x.IsString).Select(x => x.AsString)
                    : [];
            memberships.Add(new Membership(tenant.AsString, roles));
        }

        return memberships;
    }

    public static bool IsDeveloper(NIdentityUser user)
    {
        return user.OtherFields != null
            && user.OtherFields.TryGetValue(IS_DEVELOPER, out var flag)
            && flag.IsBoolean
            && flag.AsBoolean;
    }

    public static string? HomeTenantOf(NIdentityUser user)
    {
        return user.TextOf(HOME_TENANT);
    }

    /// <summary>
    /// The Tenant the account has selected, when it is still one it holds a Membership in, or when the account is the
    /// Developer, who acts in any Tenant. A selection of a Tenant it has left, or never had, counts for nothing: it is as
    /// if there were none.
    /// </summary>
    public static string? SelectedTenantOf(NIdentityUser user)
    {
        var selected = user.TextOf(SELECTED_TENANT);
        return selected != null && (IsDeveloper(user) || MembershipsOf(user).Any(x => x.TenantId == selected))
            ? selected
            : null;
    }

    /// <summary>
    /// The Tenant that what the account reads and writes belongs to, outside an Event (inside one it is the Event's): the
    /// Tenant it has selected, or its home Tenant. None when it has neither, and then nothing is read for it.
    /// </summary>
    public static string? CurrentTenantOf(NIdentityUser user)
    {
        return SelectedTenantOf(user) ?? HomeTenantOf(user);
    }
}
