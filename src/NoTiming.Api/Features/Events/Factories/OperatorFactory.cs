using NTS.Domain.Core.Aggregates;

namespace NoTiming.Api.Features.Events.Factories;

public static class OperatorFactory
{
    public static Operator Create(NTS.Domain.Setup.Aggregates.ConfigureEvents.Operator setupOperator, Guid eventId)
    {
        return new Operator(eventId, setupOperator.User.Id, setupOperator.Role);
    }
}
