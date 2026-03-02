using Not.Domain.Abstractions;
using Not.Krud.Abstractions;

namespace Not.Krud.Models;

public abstract record KrudFormModel<T> : IKrudModel<T>, IKrudFormModel
    where T : IEntity
{
    public abstract void MapFrom(T entity);
    // Fixes build: if you remove this Csharpier puts a space which fails the build for NA0004
    protected abstract T MapTo();

    public int? Id { get; set; }

    public T MapToEntity()
    {
        var entity = MapTo();
        Id = entity.Id;
        return entity;
    }
}
