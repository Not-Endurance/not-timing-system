namespace NTS.Domain.Core.Objects;

public static class SnapshotAccessPolicy
{
    public static readonly OfficialRole[] AllowedOfficialRoles =
    [
        OfficialRole.Steward,
        OfficialRole.ChiefSteward,
        OfficialRole.GroundJury,
        OfficialRole.GroundJuryPresident,
    ];

    public static bool CanWriteAsOfficial(OfficialRole role)
    {
        return AllowedOfficialRoles.Contains(role);
    }

    public static bool CanWriteAsOperator(OfficialRole role)
    {
        return role == OfficialRole.Steward;
    }
}
