using NC.Domain.Exceptions;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Setup.Aggregates;
using SetupOperator = NoTiming.Domain.Setup.Aggregates.ConfigureEvents.Operator;

namespace NoTiming.Domain.Setup.Tests.Aggregates.ConfigureEvents;

public sealed class OperatorTests
{
    [Fact]
    public void Constructor_WhenUserIsNull_ShouldThrowDomainPropertyException()
    {
        Assert.Throws<DomainPropertyException>(() => new SetupOperator(null));
    }

    [Fact]
    public void Constructor_WhenRoleIsGroundJuryPresident_ShouldAssignSteward()
    {
        var user = CreateUser();

        var @operator = new SetupOperator(user, role: OfficialRole.GroundJuryPresident);

        Assert.Equal(OfficialRole.Steward, @operator.Role);
    }

    static User CreateUser()
    {
        return new User("operator@example.test", "Operator User", id: 11);
    }
}
