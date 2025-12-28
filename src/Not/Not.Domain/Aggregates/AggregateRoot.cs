using Not.Extensions;

namespace Not.Domain.Aggregates;

// TODO: consider renaming Id to AggregateId and storing it as something other than Mongo's _id. Store only Document.Id as _id
public abstract class AggregateRoot : Aggregate, IEquatable<AggregateRoot>, IAggregateRoot
{
    public static bool operator ==(AggregateRoot? left, AggregateRoot? right)
    {
        return left?.IsEqual(right) ?? right is null;
    }

    public static bool operator !=(AggregateRoot? left, AggregateRoot? right)
    {
        return !(left == right);
    }

    protected AggregateRoot(int id)
    {
        Id = id;
    }

    // TODO: use DomainObject for ID, do private set
    public int Id { get; }

    protected static int GenerateId()
    {
        return DomainModelHelper.GenerateId();
    }

    public bool Equals(AggregateRoot? other)
    {
        return IsEqual(other);
    }

    public override bool Equals(object? other)
    {
        return IsEqual(other);
    }

    public override int GetHashCode()
    {
        return Id;
    }

    public override string ToString()
    {
        throw new NotImplementedException($"'{GetType().Name}' is AggregateRoot and is required to override ToString() to provide short info");
    }

    bool IsEqual(object? other)
    {
        if (other is null or not AggregateRoot)
        {
            return false;
        }
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        return GetHashCode() == other.GetHashCode() && GetType() == other.GetType();
    }
}
