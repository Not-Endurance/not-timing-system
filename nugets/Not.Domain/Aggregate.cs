using Not.Domain.Abstractions;

namespace Not.Domain;

public abstract class Aggregate : Entity, IAggregate
{
    /// <summary>
    /// Provide <paramref name="id"/> when updating state null to generate it
    /// </summary>
    /// <param name="id">Id, generated when null</param>
    protected Aggregate(int? id) : base(id) 
    {
    }
}
