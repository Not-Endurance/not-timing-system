using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;
using SetupOperator = NTS.Domain.Setup.Aggregates.ConfigureEvents.Operator;

namespace NTS.Domain.Setup.Tests.Aggregates;

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
