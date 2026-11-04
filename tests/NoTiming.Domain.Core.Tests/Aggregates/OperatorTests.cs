using NC.Domain.Exceptions;
using CoreOperator = NoTiming.Domain.Core.Aggregates.Operator;

namespace NoTiming.Domain.Core.Tests.Aggregates;

public sealed class OperatorTests
{
    [Fact]
    public void Constructor_WhenUserIdIsNull_ShouldThrowDomainPropertyException()
    {
        Assert.Throws<DomainPropertyException>(() => new CoreOperator(eventId: 1, userId: null));
    }
}
