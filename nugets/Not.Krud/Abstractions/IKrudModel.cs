using Not.Domain.Abstractions;

namespace Not.Krud.Abstractions;

public interface IKrudModel<T>
    where T : IEntity
{
    void MapFrom(T entity);
    T MapToEntity();
}
