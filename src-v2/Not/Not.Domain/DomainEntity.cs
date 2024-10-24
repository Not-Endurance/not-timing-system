using Not.Random;
using System.Diagnostics.CodeAnalysis;

namespace Not.Domain;

public abstract class DomainEntity : IEquatable<DomainEntity>, IIdentifiable
{
    protected DomainEntity(int id)
    {
        Id = id;
    }

    // TODO: use DomainObject for ID, do private set
    public int Id { get; }

    public static bool operator == (DomainEntity? left, DomainEntity? right)
        => left?.IsEqual(right) ?? right is null;
    public static bool operator !=(DomainEntity? left, DomainEntity? right)
        => !(left == right);

    public bool Equals(DomainEntity? other)
        => this.IsEqual(other);
    public override bool Equals(object? other)
        => this.IsEqual(other);

    private bool IsEqual(object? other)
    {
        if (other == null || other is not DomainEntity)
        {
            return false;
        }
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        return this.GetHashCode() == other.GetHashCode()
            && this.GetType() == other.GetType();
    }

    public override int GetHashCode()
        => this.Id;

	public override string ToString()
	{
        throw new NotImplementedException($"'{this.GetType().Name}' has to override ToString() to provide short info");
	}

    protected string Combine(params object?[] values)
    {
        return string.Join(" | ", values.Where(x => x != null));
    }

    protected static int GenerateId()
    {
        return RandomHelper.GenerateUniqueInteger();
    }

    protected static T NotDefault<T>(string field, T value)
        where T : struct
    {
        if (value.Equals(default))
        {
            throw GetRequiredException(field);
        }
        return value;
    }

    protected static T Required<T>(string field, T? value)
        where T : struct
    {
        return value ?? throw GetRequiredException(field);
    }

    [return: NotNull]
    protected static T Required<T>(string field, T? instance)
        where T : class
    {
        return instance ?? throw GetRequiredException(field);
    }

    [return: NotNull]
    protected static string Required(string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw GetRequiredException(field);
        }
        return value;
    }

    static DomainException GetRequiredException(string field)
    {
        return new DomainException(field, "Please provide value");
    }
}
