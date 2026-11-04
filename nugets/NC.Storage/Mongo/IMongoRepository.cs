using Microsoft.AspNetCore.OData.Query;
using NC.Application.CRUD.Ports;

namespace NC.Storage.Mongo;

public interface IMongoRepository<T> : IRepository<T>
{
    Task<IEnumerable<T>> ReadMany(ODataQueryOptions<T> options);
    Task Delete(int id);
}
