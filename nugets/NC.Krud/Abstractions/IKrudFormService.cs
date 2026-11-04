using NC.Application.CRUD.Ports;

namespace NC.Krud.Abstractions;

public interface IKrudFormService<TModel> : ICreate<TModel>, IUpdate<TModel>
    where TModel : IKrudFormModel, new() { }
