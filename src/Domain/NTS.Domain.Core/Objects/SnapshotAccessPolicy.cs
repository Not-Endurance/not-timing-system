using NTS.Domain.Access;

namespace NTS.Domain.Core.Objects;

public static class SnapshotAccessPolicy
{
    /// <summary>The roles of an Official that may send a Snapshot: the one list of the access policy (ADR-0012).</summary>
    public static readonly OfficialRole[] AllowedOfficialRoles = [.. AccessPolicy.SnapshotOfficialRoles];

    public static bool CanWriteAsOfficial(OfficialRole role)
    {
        return AllowedOfficialRoles.Contains(role);
    }

    public static bool CanWriteAsOperator(OfficialRole role)
    {
        return role == OfficialRole.Steward;
    }
}
