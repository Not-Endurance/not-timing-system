using NTS.Domain.Enums;

namespace NTS.Domain.Access;

/// <summary>
/// What an account has been granted on one Event: Snapshot access as an Operator (never named in documents), and the
/// roles of the Officials it is linked to (named in documents). The Main Operator is not a grant: the Event names it.
/// </summary>
public sealed class CallerGrants
{
    public static CallerGrants AsOperator()
    {
        return new CallerGrants(true, []);
    }

    public static CallerGrants AsOfficial(params OfficialRole[] roles)
    {
        return new CallerGrants(false, roles);
    }

    /// <summary>
    /// What the grants an account holds on one Event add up to. A pending grant, which has no account yet, adds nothing.
    /// </summary>
    public static CallerGrants Of(IEnumerable<EventGrant> grants)
    {
        var held = grants.Where(x => !x.IsPending).ToList();
        return new CallerGrants(
            held.Any(x => x.Kind == GrantKind.Operator),
            held.Where(x => x.Kind == GrantKind.Official && x.OfficialRole != null).Select(x => x.OfficialRole!.Value)
        );
    }

    public CallerGrants(bool isOperator, IEnumerable<OfficialRole> officialRoles)
    {
        IsOperator = isOperator;
        OfficialRoles = [.. officialRoles.Distinct()];
    }

    public static CallerGrants None { get; } = new(false, []);

    public bool IsOperator { get; }
    public IReadOnlyCollection<OfficialRole> OfficialRoles { get; }

    /// <summary>Staff: Operators and Officials together, the people who work an Event.</summary>
    public bool IsStaff => IsOperator || OfficialRoles.Count > 0;

    /// <summary>Whether a grant lets the account send a Snapshot: any Operator, an Official of a role that may.</summary>
    public bool MaySendSnapshot => IsOperator || OfficialRoles.Any(AccessPolicy.SnapshotOfficialRoles.Contains);
}
