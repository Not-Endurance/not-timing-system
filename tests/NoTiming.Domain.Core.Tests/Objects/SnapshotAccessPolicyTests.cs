using NoTiming.Domain.Core.Objects;
using NoTiming.Domain.Enums;

namespace NoTiming.Domain.Core.Tests.Objects;

public sealed class SnapshotAccessPolicyTests
{
    [Theory]
    [InlineData(OfficialRole.Steward)]
    [InlineData(OfficialRole.ChiefSteward)]
    [InlineData(OfficialRole.GroundJury)]
    [InlineData(OfficialRole.GroundJuryPresident)]
    public void CanWriteAsOfficial_WhenRoleCanWrite_ShouldReturnTrue(OfficialRole role)
    {
        Assert.True(SnapshotAccessPolicy.CanWriteAsOfficial(role));
    }

    [Fact]
    public void CanWriteAsOfficial_WhenRoleCannotWrite_ShouldReturnFalse()
    {
        Assert.False(SnapshotAccessPolicy.CanWriteAsOfficial(OfficialRole.VeterinaryCommissionMember));
    }

    [Fact]
    public void CanWriteAsOperator_WhenRoleIsSteward_ShouldReturnTrue()
    {
        Assert.True(SnapshotAccessPolicy.CanWriteAsOperator(OfficialRole.Steward));
    }

    [Fact]
    public void CanWriteAsOperator_WhenRoleIsChiefSteward_ShouldReturnFalse()
    {
        Assert.False(SnapshotAccessPolicy.CanWriteAsOperator(OfficialRole.ChiefSteward));
    }
}
