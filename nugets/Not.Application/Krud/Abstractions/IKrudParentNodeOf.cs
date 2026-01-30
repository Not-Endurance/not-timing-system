using Not.Domain;
using Not.Domain.Krud;

namespace Not.Application.Krud.Abstractions;

public interface IKrudParentNodeOf<T> : IParent<T>, IKrudNodeSetter
    where T : Entity { }
