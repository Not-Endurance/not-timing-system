using NC.Domain.Abstractions;

namespace NC.Krud.Abstractions;

public interface IKrudModel<T>
    where T : IEntity
{
    void MapFrom(T entity);
    T MapToEntity();
}
