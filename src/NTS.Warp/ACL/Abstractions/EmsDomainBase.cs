namespace NTS.Warp.ACL.Abstractions;

public abstract class EmsDomainBase<TException> : IEmsDomain, IEquatable<EmsDomainBase<TException>>, IEmsIdentifiable
    where TException : EmsDomainExceptionBase, new()
{
    protected const string GENERATE_ID = "GenerateIdFlag";

    public static bool operator ==(EmsDomainBase<TException>? one, EmsDomainBase<TException>? two)
    {
        return one is null ? two is null : one.Equals(two);
    }

    public static bool operator !=(EmsDomainBase<TException>? one, EmsDomainBase<TException>? two) => !(one == two);

    // Empty constructor is used by mapping for existing (in the database) entries
    protected EmsDomainBase() { }

    protected EmsDomainBase(string _) // Unused variable is needed mark the constructor which generates Id. That constructor should ONLY be used when creating NEW (no database entry) objects
    {
        Id = EmsDomainIdProvider.Generate();
    }

    protected EmsValidator<TException> Validator { get; } = new();
    public int Id { get; protected init; } // Keep setter for mapping

    public override bool Equals(object? other)
    {
        return IsEqual(other);
    }

    public bool Equals(IEmsIdentifiable identifiable)
    {
        return IsEqual(identifiable);
    }

    public bool Equals(EmsDomainBase<TException>? domain)
    {
        return IsEqual(domain);
    }

    public override int GetHashCode()
    {
        return Id;
    }

    bool IsEqual(object? other)
    {
        if (other is not IEmsIdentifiable identifiable)
        {
            return false;
        }
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        if (Id == identifiable.Id)
        {
            return true;
        }
        if (GetType() != other.GetType())
        {
            return false;
        }
        return GetHashCode() == other.GetHashCode();
    }
}
