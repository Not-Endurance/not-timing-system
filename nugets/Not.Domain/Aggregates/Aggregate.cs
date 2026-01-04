using Not.Extensions;

namespace Not.Domain.Aggregates;

public abstract class Aggregate : InLineAggregateValidator, IAggregate
{
    protected string Combine(params object?[] values)
    {
        return DomainModelHelper.Combine(values);
    }
}
