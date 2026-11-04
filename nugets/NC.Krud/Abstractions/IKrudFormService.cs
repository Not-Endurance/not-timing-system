using Not.Application.CRUD.Ports;

namespace Not.Krud.Abstractions;

public interface IKrudFormService<TModel> : ICreate<TModel>, IUpdate<TModel>
    where TModel : IKrudFormModel, new() { }
