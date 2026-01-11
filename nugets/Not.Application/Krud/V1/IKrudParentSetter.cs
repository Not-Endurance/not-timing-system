using Not.Domain.Aggregates;

namespace Not.Application.Krud.V1;

public interface IKrudParentSetter
{
    void Set(object aggregate);
}
