using Not.Domain.Exceptions;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;
using CoreOperator = NTS.Domain.Core.Aggregates.Operator;
using SetupOperator = NTS.Domain.Setup.Aggregates.ConfigureEvents.Operator;

namespace NTS.Tests.Unit.Domain;

public sealed class OperatorTests
{
    [Fact]
    public void Setup_operator_requires_user()
    {
        Assert.Throws<DomainPropertyException>(() => new SetupOperator(null));
    }

    [Fact]
    public void Setup_operator_role_is_steward()
    {
        var user = CreateUser();

        var @operator = new SetupOperator(user, role: OfficialRole.GroundJuryPresident);

        Assert.Equal(OfficialRole.Steward, @operator.Role);
    }

    [Fact]
    public void Core_operator_requires_user_id()
    {
        Assert.Throws<DomainPropertyException>(() => new CoreOperator(eventId: 1, userId: null));
    }

    [Fact]
    public void Configure_event_keeps_operators_separate_from_officials()
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        var official = new Official("Ground Jury", null, OfficialRole.GroundJury, id: 101);
        var @operator = new SetupOperator(CreateUser(), id: 201);

        var setupEvent = new ConfigureEvent(
            "Event",
            "Sofia",
            country,
            null,
            [],
            [official],
            [],
            [],
            id: 301,
            operators: [@operator]
        );

        Assert.Equal([official], setupEvent.Officials);
        Assert.Equal([@operator], setupEvent.Operators);
    }

    static User CreateUser()
    {
        return new User("operator@example.test", "Operator User", id: 11);
    }
}
