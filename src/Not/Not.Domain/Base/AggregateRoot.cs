using System.Diagnostics.CodeAnalysis;
using Not.Domain.Exceptions;
using Not.Extensions;
using static Not.Localization.NStrings;

namespace Not.Domain.Base;

// TODO: consider renaming Id to AggregateId and storing it as something other than Mongo's _id. Store only Document.Id as _id
public abstract class AggregateRoot : IEquatable<AggregateRoot>, IAggregateRoot
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

    protected static string Combine(params object?[] values)
    {
        return DomainModelHelper.Combine(values);
    }

    protected static int GenerateId()
    {
        return DomainModelHelper.GenerateId();
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
    protected static string Required(string field, [NotNull] string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw GetRequiredException(field);
        }
        return value;
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
        throw new NotImplementedException($"'{GetType().Name}' has to override ToString() to provide short info");
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

    static DomainPropertyException GetRequiredException(string field)
    {
        return new DomainPropertyException(field, Field_is_required_string);
    }
}
