using Not.Domain.Exceptions;
using CoreOperator = NTS.Domain.Core.Aggregates.Operator;

namespace NTS.Domain.Core.Tests.Aggregates;

public sealed class OperatorTests
{
    [Fact]
    public void Constructor_WhenUserIdIsNull_ShouldThrowDomainPropertyException()
    {
        Assert.Throws<DomainPropertyException>(() => new CoreOperator(eventId: 1, userId: null));
    }
}
