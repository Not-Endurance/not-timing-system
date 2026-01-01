using Not.Extensions;

namespace Not.Domain.Base;

// TODO: use same validation as in DomainEntity. Maybe drop DomainObject being a record entirely?
public abstract record DomainObject
{
    protected string Combine(params object?[] values)
    {
        return DomainModelHelper.Combine(values);
    }
}
