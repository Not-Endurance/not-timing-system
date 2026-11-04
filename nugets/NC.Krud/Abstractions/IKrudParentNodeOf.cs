using NC.Domain;
using NC.Domain.Krud;

namespace NC.Krud.Abstractions;

public interface IKrudParentNodeOf<T> : IKrudParent<T>, IKrudNodeSetter
    where T : Entity { }
