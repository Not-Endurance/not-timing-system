using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Setup.Aggregates;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;
using SetupOperator = NoTiming.Domain.Setup.Aggregates.ConfigureEvents.Operator;

namespace NoTiming.Domain.Setup.Tests.Aggregates;

public sealed class ConfigureEventTests
{
    [Fact]
    public void Constructor_WhenOperatorsAndOfficialsAreProvided_ShouldKeepThemSeparate()
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
