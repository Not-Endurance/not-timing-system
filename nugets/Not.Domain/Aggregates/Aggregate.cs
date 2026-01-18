using Not.Extensions;

namespace Not.Domain.Aggregates;

public abstract class Aggregate : Entity, IAggregate
{
    protected Aggregate(int id)
        : base(id)
    {
        Id = id;
    }

    // TODO: use DomainObject for ID, do private set
    public int Id { get; }

    protected static int GenerateId()
    {
        return DomainModelHelper.GenerateId();
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}
