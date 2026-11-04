using NC.Domain.Abstractions;
using NC.Krud.Abstractions;

namespace NC.Krud.Models;

public abstract record KrudFormModel<T> : IKrudModel<T>, IKrudFormModel
    where T : IEntity
{
    public abstract void MapFrom(T entity);

    protected abstract T MapTo();

    public int? Id { get; set; }

    public T MapToEntity()
    {
        var entity = MapTo();
        Id = entity.Id;
        return entity;
    }
}
