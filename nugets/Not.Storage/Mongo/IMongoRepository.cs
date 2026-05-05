using Microsoft.AspNetCore.OData.Query;
using Not.Application.CRUD.Ports;

namespace Not.Storage.Mongo;

public interface IMongoRepository<T> : IRepository<T>
{
    Task<IEnumerable<T>> ReadMany(ODataQueryOptions<T> options);
    Task Delete(int id);
}
