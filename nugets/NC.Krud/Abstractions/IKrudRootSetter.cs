using NC.Domain;
using NC.Krud.Graph;

namespace NC.Krud.Abstractions;

public interface IKrudRootSetter<T>
    where T : Aggregate
{
    void Set(KrudNode node);
}
