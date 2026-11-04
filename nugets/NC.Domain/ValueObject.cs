using NC.Extensions;

namespace NC.Domain;

public abstract record ValueObject
{
    protected string Combine(params object?[] values)
    {
        return DomainModelHelper.Combine(values);
    }
}
