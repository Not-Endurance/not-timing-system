using Not.Domain;
using Not.Domain.Aggregates;

namespace Not.Application.Krud.Abstractions;

public interface IKrudParentNodeOf<T> : IParent<T>, IKrudNodeSetter
    where T : Aggregate { }
