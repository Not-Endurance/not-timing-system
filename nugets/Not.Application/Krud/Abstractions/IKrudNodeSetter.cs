using Not.Domain.Aggregates;

namespace Not.Application.Krud.Abstractions;

public interface IKrudNodeSetter
{
    void SetParent(object aggregate);
}
