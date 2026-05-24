using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;

namespace NTS.Tests.Unit.Domain;

public sealed class SnapshotAccessPolicyTests
{
    [Theory]
    [InlineData(OfficialRole.Steward)]
    [InlineData(OfficialRole.ChiefSteward)]
    [InlineData(OfficialRole.GroundJury)]
    [InlineData(OfficialRole.GroundJuryPresident)]
    public void CanWriteAsOfficial_allows_writing_roles(OfficialRole role)
    {
        Assert.True(SnapshotAccessPolicy.CanWriteAsOfficial(role));
    }

    [Fact]
    public void CanWriteAsOfficial_rejects_non_writing_role()
    {
        Assert.False(SnapshotAccessPolicy.CanWriteAsOfficial(OfficialRole.VeterinaryCommissionMember));
    }

    [Fact]
    public void CanWriteAsOperator_only_allows_steward()
    {
        Assert.True(SnapshotAccessPolicy.CanWriteAsOperator(OfficialRole.Steward));
        Assert.False(SnapshotAccessPolicy.CanWriteAsOperator(OfficialRole.ChiefSteward));
    }
}
