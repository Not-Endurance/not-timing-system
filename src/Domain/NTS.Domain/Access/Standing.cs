using NTS.Domain.Enums;

namespace NTS.Domain.Access;

/// <summary>
/// Where a caller stands at one place: its Level and, for Staff, whether its grants let it record. A Console Operator and
/// Root record whatever their grants say.
/// </summary>
public sealed class Standing
{
    public static Standing Of(AccessLevel level, bool mayRecord = false)
    {
        return new Standing(level, mayRecord);
    }

    /// <summary>Where an account stands at an Event of a Tenant, given the grants it holds on that Event.</summary>
    public static Standing AtEvent(Roles roles, string tenantId, IEnumerable<EventGrant> grants)
    {
        if (roles.IsRootAt(tenantId))
        {
            return Of(AccessLevel.Root);
        }

        var held = grants.Where(x => !x.IsPending).ToList();
        if (held.Any(x => x.Kind == GrantKind.ConsoleOperator))
        {
            return Of(AccessLevel.ConsoleOperator);
        }

        return held.Count == 0 ? Of(AccessLevel.Account) : Of(AccessLevel.Staff, held.Any(RecordsSnapshots));
    }

    /// <summary>
    /// Where an account stands at a Tenant: Root there if it is the Developer or a Tenant Root of it, a Console Operator if it
    /// is one of an Event of the Tenant that has not ended.
    /// </summary>
    public static Standing AtTenant(Roles roles, string tenantId, bool isConsoleOperatorOfAnOpenEvent)
    {
        if (roles.IsRootAt(tenantId))
        {
            return Of(AccessLevel.Root);
        }

        return Of(isConsoleOperatorOfAnOpenEvent ? AccessLevel.ConsoleOperator : AccessLevel.Account);
    }

    /// <summary>
    /// Where an account stands across Tenants: the Developer alone is Root; the people who set an Event up, a Tenant Root of any
    /// Tenant or a Console Operator of any Event that has not ended, stand as Console Operators.
    /// </summary>
    public static Standing AtPlatform(Roles roles, bool isConsoleOperatorOfAnOpenEvent)
    {
        if (roles.IsDeveloper)
        {
            return Of(AccessLevel.Root);
        }

        return Of(
            isConsoleOperatorOfAnOpenEvent || roles.SetsEventsUp ? AccessLevel.ConsoleOperator : AccessLevel.Account
        );
    }

    Standing(AccessLevel level, bool mayRecord)
    {
        Level = level;
        MayRecord = mayRecord || level >= AccessLevel.ConsoleOperator;
    }

    public static Standing Anonymous { get; } = new(AccessLevel.Anonymous, false);

    /// <summary>A signed-in account that holds nothing at the place: what a route that asks only that somebody is signed in needs.</summary>
    public static Standing Account { get; } = new(AccessLevel.Account, false);

    /// <summary>The roles of an Official that may send a Snapshot; any Operator may too. The other Officials see how times were recorded and send none.</summary>
    public static IReadOnlyList<OfficialRole> SnapshotOfficialRoles { get; } =
        [OfficialRole.Steward, OfficialRole.ChiefSteward, OfficialRole.GroundJury, OfficialRole.GroundJuryPresident];

    public AccessLevel Level { get; }
    public bool MayRecord { get; }

    static bool RecordsSnapshots(EventGrant grant)
    {
        return grant.Kind == GrantKind.Operator
            || (grant.OfficialRole is { } role && SnapshotOfficialRoles.Contains(role));
    }
}
