using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Application.Factories;

public static class OperatorFactory
{
    public static Operator Create(Domain.Setup.Aggregates.ConfigureEvents.Operator setupOperator, int eventId)
    {
        return new Operator(eventId, setupOperator.User.Id, setupOperator.Role);
    }
}
