using NTS.Domain.Core.Aggregates;

namespace NTS.Application.Factories;

public static class OperatorFactory
{
    public static Operator Create(Domain.Setup.Aggregates.ConfigureEvents.Operator setupOperator, int eventId)
    {
        return new Operator(eventId, setupOperator.User.Id, setupOperator.Role);
    }
}
