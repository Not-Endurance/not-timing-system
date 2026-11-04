using Not.Domain;
using Not.Domain.Krud;

namespace Not.Krud.Abstractions;

public interface IKrudParentNodeOf<T> : IKrudParent<T>, IKrudNodeSetter
    where T : Entity { }
