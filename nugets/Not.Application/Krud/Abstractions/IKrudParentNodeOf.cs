using Not.Domain;
using Not.Domain.Aggregates;

namespace Not.Application.Krud.Abstractions;

public interface IKrudParentNodeOf<T> : IKrudParent<T>, IKrudNodeSetter
    where T : Entity { }
