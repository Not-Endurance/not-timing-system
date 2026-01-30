using Not.Extensions;

namespace Not.Domain;

public abstract record ValueObject
{
    protected string Combine(params object?[] values)
    {
        return DomainModelHelper.Combine(values);
    }
}
